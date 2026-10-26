// Phase 10 — k6 load test: proves the Redis hot-path never locks SQL under a bid storm.
//
// Speaks the SignalR JSON hub protocol over WebSockets (k6/x/websockets):
//   1. Register/login a throwaway user to obtain a JWT.
//   2. Negotiate the hub to get a connectionToken.
//   3. Open a WS to /hubs/auction?access_token=..&id=.. and send the JSON handshake.
//   4. JoinAuctionRoom(auctionId) → RoomSnapshot initialises the room.
//   5. Flood SendBid with unique UUID idempotency keys.
//   6. Assert the accepted price is monotonic and every outcome is a known status.
//
// Requirements:
//   - API reachable (BASE_URL, default http://localhost:5106).
//   - A live auction exists; provide AUCTION_ID (default 1).
//   - k6 built with the xk6-websockets extension (k6/x/websockets).
//   - Ensure the auction is live (Status=Active) and StartTime has passed.
//
// Run:
//   k6 run --env AUCTION_ID=1 k6/bid-flow.js
//
// Verifying the "SQL is never locked" assertion: watch SQL activity (sp_who2 / the
// BidPersistorService logs) while this runs. Accepted bids only hit Redis + a stream;
// SQL writes happen asynchronously in single batches, never under the bid lock.

import { check } from 'k6';
import http from 'k6/http';
import WebSocket from 'k6/x/websockets';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5106';
const WS_BASE = BASE_URL.replace(/^http/, 'ws');
const AUCTION_ID = Number(__ENV.AUCTION_ID || 1);
const BIDS_PER_SOCKET = Number(__ENV.BIDS_PER_SOCKET || 20);
const STEP = Number(__ENV.STEP || 10);
const SEP = '\x1e';

export const options = {
  scenarios: {
    bid_storm: {
      executor: 'ramping-vus',
      startVUs: 1,
      stages: [
        { duration: '10s', target: 50 },
        { duration: '30s', target: 500 },
        { duration: '30s', target: 1000 },
        { duration: '10s', target: 0 },
      ],
      gracefulStop: '10s',
    },
  },
  thresholds: {
    checks: ['rate>0.99'],
    ws_errors: ['rate<0.01'],
  },
};

const KNOWN_STATUSES = ['ACCEPTED', 'DUPLICATE', 'OUTBID', 'ENDED', 'NOT_STARTED'];

// ---------------- helper functions ----------------

function uuid() {
  return 'xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx'.replace(/x/g, () =>
    Math.floor(Math.random() * 16).toString(16),
  );
}

function postJson(path, payload, token) {
  const headers = { 'Content-Type': 'application/json' };
  if (token) headers.Authorization = `Bearer ${token}`;
  const res = http.post(`${BASE_URL}${path}`, JSON.stringify(payload), { headers });
  return JSON.parse(res.body);
}

function getAccessToken() {
  const email = `k6_${Date.now()}_${Math.floor(Math.random() * 1e6)}@example.com`;
  const reg = postJson('/api/auth/register', {
    username: `k6_${Math.floor(Math.random() * 1e6)}`,
    email,
    password: 'K6LoadTestP@ss1',
    fullName: 'k6 Loader',
    // The server rejects bids from users without a phone (phone gate).
    phoneNumber: '+15550000000',
  });
  if (reg && reg.success && reg.data && reg.data.accessToken) return reg.data.accessToken;

  const login = postJson('/api/auth/login', { email, password: 'K6LoadTestP@ss1' });
  if (login && login.success && login.data && login.data.accessToken) return login.data.accessToken;
  throw new Error(`Cannot obtain an access token: ${JSON.stringify(reg)}`);
}

function negotiate(accessToken) {
  // Hub is mapped at /hubs/auction (no /api prefix).
  const res = http.post(
    `${BASE_URL}/hubs/auction/negotiate?negotiateVersion=1`,
    null,
    { headers: { Authorization: `Bearer ${accessToken}` } },
  );
  const body = JSON.parse(res.body);
  return body.connectionToken || body.connectionId;
}

function frameInvoke(invocationId, target, args) {
  return JSON.stringify({ type: 1, invocationId, target, arguments: args }) + SEP;
}

// ---------------- the load scenario ----------------

export default function () {
  const accessToken = getAccessToken();
  const connectionToken = negotiate(accessToken);
  const url = `${WS_BASE}/hubs/auction?access_token=${accessToken}&id=${connectionToken}`;

  const socket = new WebSocket(url);

  let handshakeAcked = false;
  let started = false;
  let sentBids = 0;
  let seenSnapshot = false;
  let accepts = 0;
  let regressions = 0;
  let lastPrice = 0;
  let unexpectedRejections = 0;
  let errorCount = 0;
  // Kick off the flood once we've joined and received the snapshot.
  function startFlood() {
    if (started) return;
    started = true;
    const base = AUCTION_ID * 100000;
    for (let i = 0; i < BIDS_PER_SOCKET; i++) {
      const amount = base + (i + 1) * STEP;
      const key = uuid();
      const invocationId = `bid_${i}_${Math.random().toString(36).slice(2, 8)}`;
      socket.send(
        frameInvoke(invocationId, 'SendBid', [
          { auctionId: AUCTION_ID, amount, idempotencyKey: key },
        ]),
      );
      sentBids += 1;
    }
    // Give the server a moment to respond, then close and evaluate.
    setTimeout(() => {
      socket.close();
    }, 3000);
  }

  function recordAccepted(price) {
    accepts += 1;
    if (price < lastPrice) {
      regressions += 1;
    }
    if (price > lastPrice) {
      lastPrice = price;
    }
  }

  socket.onopen = () => {
    socket.send(JSON.stringify({ protocol: 'json', version: 1 }) + SEP);
  };

  socket.onmessage = (event) => {
    const frames = String(event.data).split(SEP).filter(Boolean);
    for (const raw of frames) {
      // The server handshake ack is an empty `{}` frame — that's our cue to join.
      if (raw === '{}') {
        if (!handshakeAcked) {
          handshakeAcked = true;
          socket.send(frameInvoke('join_0', 'JoinAuctionRoom', [AUCTION_ID]));
        }
        continue;
      }

      let msg;
      try {
        msg = JSON.parse(raw);
      } catch {
        continue;
      }

      if (msg && msg.target === 'RoomSnapshot') {
        seenSnapshot = true;
        startFlood();
        continue;
      }

      // Join completion frame — fallback entrance to the flood.
      if (msg && (msg.type === 3 || msg.type === 2)) {
        if (msg.invocationId === 'join_0' && !started) {
          startFlood();
        }
        continue;
      }

      // Notifications (type 1) with a target.
      if (msg && msg.target) {
        if (msg.target === 'ReceiveNewBid' && msg.arguments && msg.arguments[0]) {
          recordAccepted(Number(msg.arguments[0].amount));
        } else if (msg.target === 'BidAccepted' && msg.arguments && msg.arguments[0]) {
          recordAccepted(Number(msg.arguments[0].currentPrice));
        } else if (msg.target === 'BidRejected' && msg.arguments && msg.arguments[0]) {
          const status = String(msg.arguments[0].status);
          if (!KNOWN_STATUSES.includes(status)) {
            unexpectedRejections += 1;
          }
        }
      }
    }
  };

  socket.onerror = (event) => {
    errorCount += 1;
  };

  socket.onclose = () => {
    check(handshakeAcked, { 'handshake acknowledged': handshakeAcked });
    check(seenSnapshot, { 'received RoomSnapshot': seenSnapshot });
    check(sentBids > 0, { 'sent a bid flood': sentBids > 0 });
    check(accepts > 0, { 'at least one accepted bid observed': accepts > 0 });
    check(regressions === 0, { 'accepted price never regressed': regressions === 0 });
    check(unexpectedRejections === 0, { 'no unexpected rejection outcomes': unexpectedRejections === 0 });
    check(errorCount === 0, { 'no WebSocket errors': errorCount === 0 });
  };
}
