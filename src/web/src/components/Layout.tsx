import { Link, Outlet, useNavigate } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'

function NavLink({ to, children }: { to: string; children: React.ReactNode }) {
  return (
    <Link
      to={to}
      className="rounded-lg px-3 py-1.5 text-sm text-slate-300 hover:bg-white/5 hover:text-white transition"
    >
      {children}
    </Link>
  )
}

export default function Layout() {
  const { isAuthenticated, user, logout } = useAuth()
  const navigate = useNavigate()

  function handleLogout() {
    logout()
    navigate('/')
  }

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100">
      <header className="sticky top-0 z-30 border-b border-white/10 bg-slate-950/80 backdrop-blur">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-4 py-3">
          <Link to="/" className="text-lg font-bold tracking-tight text-indigo-300">
            ToysAuctionHouse
          </Link>
          <nav className="flex items-center gap-1">
            <NavLink to="/">Catalog</NavLink>
            {isAuthenticated && <NavLink to="/sell">Sell</NavLink>}
            {isAuthenticated && <NavLink to="/orders">Orders</NavLink>}
            {isAuthenticated && user && <NavLink to="/me">{user.username}</NavLink>}
            {isAuthenticated ? (
              <button
                onClick={handleLogout}
                className="rounded-lg px-3 py-1.5 text-sm text-slate-300 hover:bg-white/5 hover:text-white transition"
              >
                Logout
              </button>
            ) : (
              <NavLink to="/login">Login</NavLink>
            )}
          </nav>
        </div>
      </header>
      <main className="mx-auto max-w-6xl px-4 py-6">
        <Outlet />
      </main>
    </div>
  )
}
