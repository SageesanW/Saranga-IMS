/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./src/pages/**/*.{js,ts,jsx,tsx,mdx}",
    "./src/components/**/*.{js,ts,jsx,tsx,mdx}",
    "./src/app/**/*.{js,ts,jsx,tsx,mdx}",
  ],
  theme: {
    extend: {
      colors: {
        saranga: {
          DEFAULT: '#123B32',
          forest: '#0B2822',
          green: {
            50: '#F0F9F5',
            100: '#DCEFE6',
            200: '#B8DFC9',
            300: '#8ACAA6',
            400: '#5BAE81',
            500: '#389163',
            600: '#26734D',
            700: '#1B5B3C',
            800: '#123B32', // Royal Green
            900: '#0B2822', // Deep Forest
            950: '#061713',
          },
          gold: {
            50: '#FBF9F2',
            100: '#F5F0DF',
            200: '#EBDDBE',
            300: '#DEC496',
            400: '#CCA86E',
            500: '#B89A60', // Muted Luxury Gold
            600: '#9C7E47',
            700: '#7D6336',
            800: '#644F2D',
            900: '#524126',
            950: '#2E2413',
            DEFAULT: '#B89A60',
          },
          ivory: '#F8F7F4',
          surface: '#FFFFFF',
          text: {
            primary: '#202824',
            secondary: '#69736E',
            muted: '#8E9994',
          },
          border: '#E2E6E3',
          borderDark: '#CCD2CE',
        }
      },
      fontFamily: {
        sans: [
          'Inter',
          '-apple-system',
          'BlinkMacSystemFont',
          '"Segoe UI"',
          'Roboto',
          'Helvetica',
          'Arial',
          'sans-serif',
        ],
        mono: [
          'ui-monospace',
          'SFMono-Regular',
          'Menlo',
          'Monaco',
          'Consolas',
          'monospace',
        ],
      },
    },
  },
  plugins: [],
};
