/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ["./**/*.html", "./**/*.cshtml"],
  output: {
    path: "./input/public/main.css"
  },
  options: {
    minify: true
  },
  theme: {
    extend: {},
  },
  plugins: [],
}

