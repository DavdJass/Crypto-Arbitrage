import { Dashboard } from './components/Dashboard';
import { useTheme } from './hooks/useTheme';
import './App.css';

export default function App() {
  // Inicializa el tema al cargar la aplicación
  useTheme();

  return <Dashboard />;
}
