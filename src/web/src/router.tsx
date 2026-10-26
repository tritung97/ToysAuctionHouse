import { createBrowserRouter } from 'react-router-dom'
import Layout from './components/Layout'
import AuctionRoomPage from './pages/AuctionRoomPage'
import CatalogPage from './pages/CatalogPage'
import LoginPage from './pages/LoginPage'
import OrdersPage from './pages/OrdersPage'
import ProfilePage from './pages/ProfilePage'
import RegisterPage from './pages/RegisterPage'
import SellPage from './pages/SellPage'

export const router = createBrowserRouter([
  {
    path: '/',
    element: <Layout />,
    children: [
      { index: true, element: <CatalogPage /> },
      { path: 'login', element: <LoginPage /> },
      { path: 'register', element: <RegisterPage /> },
      { path: 'auction/:id', element: <AuctionRoomPage /> },
      { path: 'sell', element: <SellPage /> },
      { path: 'orders', element: <OrdersPage /> },
      { path: 'me', element: <ProfilePage /> },
    ],
  },
])
