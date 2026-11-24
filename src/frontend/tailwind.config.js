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
          green: {
            50: '#F0F9F4',
            100: '#DCF1E6',
            200: '#BCE4CF',
            300: '#8ED1AF',
            400: '#59B889',
            500: '#349D6B',
            600: '#237E53',
            700: '#1B6543',
            800: '#144F35',
            900: '#0D3826',
            950: '#072016',
            DEFAULT: '#0D3826', // Royal Deep Green
          },
          gold: {
            50: '#FDFBF0',
            100: '#FAF4DB',
            200: '#F5E7B5',
            300: '#EED588',
            400: '#E4BF59',
            500: '#D4AF37', // Royal Gold
            600: '#BA9225',
            700: '#946E1E',
            800: '#7A571E',
            900: '#67471E',
            950: '#3D260D',
            DEFAULT: '#D4AF37',
          }
        }
      }
    },
  },
  plugins: [],
};
