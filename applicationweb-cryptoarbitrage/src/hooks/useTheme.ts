import { useState, useEffect, useCallback } from 'react';

type Theme = 'dark' | 'light';

const STORAGE_KEY = 'crypto-arbitrage-theme';

function getInitialTheme(): Theme {
  // 1. Preferencia guardada en localStorage
  const stored = localStorage.getItem(STORAGE_KEY);
  if (stored === 'dark' || stored === 'light') return stored;

  // 2. Preferencia del sistema operativo
  if (window.matchMedia?.('(prefers-color-scheme: light)').matches) return 'light';

  // 3. Default: oscuro
  return 'dark';
}

export function useTheme() {
  const [theme, setThemeState] = useState<Theme>(getInitialTheme);

  const applyTheme = useCallback((t: Theme) => {
    if (t === 'light') {
      document.documentElement.classList.add('light');
    } else {
      document.documentElement.classList.remove('light');
    }
  }, []);

  const toggleTheme = useCallback(() => {
    setThemeState(prev => {
      const next = prev === 'dark' ? 'light' : 'dark';
      localStorage.setItem(STORAGE_KEY, next);
      return next;
    });
  }, []);

  // Aplicar tema al montar
  useEffect(() => {
    applyTheme(theme);
  }, [theme, applyTheme]);

  // Escuchar cambios en preferencia del sistema
  useEffect(() => {
    const mq = window.matchMedia('(prefers-color-scheme: light)');
    const handler = (e: MediaQueryListEvent) => {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (!stored) {
        const newTheme = e.matches ? 'light' : 'dark';
        setThemeState(newTheme);
      }
    };
    mq.addEventListener('change', handler);
    return () => mq.removeEventListener('change', handler);
  }, []);

  // Agregar transición suave para el cambio de tema
  useEffect(() => {
    const style = document.createElement('style');
    style.textContent = `
      html.theme-transitioning,
      html.theme-transitioning *,
      html.theme-transitioning *::before,
      html.theme-transitioning *::after {
        transition: background-color 0.3s ease,
                    color 0.3s ease,
                    border-color 0.3s ease,
                    box-shadow 0.3s ease !important;
      }
    `;
    document.head.appendChild(style);

    // Activar transiciones después de un pequeño delay (para evitar flash en carga)
    const timeout = setTimeout(() => {
      document.documentElement.classList.add('theme-transitioning');
    }, 100);

    return () => {
      clearTimeout(timeout);
      document.head.removeChild(style);
    };
  }, []);

  return { theme, toggleTheme, isDark: theme === 'dark' };
}
