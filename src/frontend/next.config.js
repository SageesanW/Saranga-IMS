/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  env: {
    NEXT_PUBLIC_API_URL: process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5062/api',
  },
  async redirects() {
    return [
      {
        source: '/metal-rates',
        destination: '/rates',
        permanent: true,
      },
    ];
  },
};

module.exports = nextConfig;
