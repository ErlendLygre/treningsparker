import { Navigate, Route, Routes } from 'react-router'
import type { ReactNode } from 'react'
import Header from './components/Header'
import { useAuth } from './auth'
import MapPage from './pages/MapPage'
import LoginPage from './pages/LoginPage'
import RegisterPage from './pages/RegisterPage'
import AddParkPage from './pages/AddParkPage'
import MyParksPage from './pages/MyParksPage'
import AdminPage from './pages/AdminPage'

function RequireAuth({ children, admin = false }: { children: ReactNode; admin?: boolean }) {
  const { user } = useAuth()
  if (!user) return <Navigate to="/logg-inn" replace />
  if (admin && !user.isAdmin) return <Navigate to="/" replace />
  return children
}

export default function App() {
  return (
    <div className="app">
      <Header />
      <main className="main">
        <Routes>
          <Route path="/" element={<MapPage />} />
          <Route path="/logg-inn" element={<LoginPage />} />
          <Route path="/registrer" element={<RegisterPage />} />
          <Route path="/ny-park" element={<RequireAuth><AddParkPage /></RequireAuth>} />
          <Route path="/mine-forslag" element={<RequireAuth><MyParksPage /></RequireAuth>} />
          <Route path="/admin" element={<RequireAuth admin><AdminPage /></RequireAuth>} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </main>
    </div>
  )
}
