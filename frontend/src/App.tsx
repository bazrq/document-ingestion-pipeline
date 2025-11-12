import { useState } from 'react'

function App() {
  const [count, setCount] = useState(0)

  return (
    <div className="min-h-screen bg-gradient-to-br from-blue-50 to-indigo-100 dark:from-gray-900 dark:to-gray-800">
      <div className="container mx-auto px-4 py-16">
        <div className="max-w-2xl mx-auto text-center">
          <h1 className="text-5xl font-bold text-gray-900 dark:text-white mb-4">
            Welcome to React + Vite
          </h1>
          <p className="text-xl text-gray-600 dark:text-gray-300 mb-8">
            Built with TypeScript and Tailwind CSS
          </p>

          <div className="bg-white dark:bg-gray-800 rounded-lg shadow-lg p-8 mb-8">
            <div className="mb-6">
              <button
                onClick={() => setCount((count) => count + 1)}
                className="bg-indigo-600 hover:bg-indigo-700 text-white font-semibold py-3 px-8 rounded-lg transition duration-200 transform hover:scale-105 shadow-md"
              >
                Count is {count}
              </button>
            </div>
            <p className="text-gray-600 dark:text-gray-400">
              Click the button to increment the counter
            </p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            <div className="bg-white dark:bg-gray-800 rounded-lg shadow-md p-6">
              <div className="text-blue-600 dark:text-blue-400 text-3xl mb-2">⚡</div>
              <h3 className="font-semibold text-gray-900 dark:text-white mb-2">Fast</h3>
              <p className="text-sm text-gray-600 dark:text-gray-400">
                Lightning-fast HMR with Vite
              </p>
            </div>

            <div className="bg-white dark:bg-gray-800 rounded-lg shadow-md p-6">
              <div className="text-indigo-600 dark:text-indigo-400 text-3xl mb-2">🎨</div>
              <h3 className="font-semibold text-gray-900 dark:text-white mb-2">Styled</h3>
              <p className="text-sm text-gray-600 dark:text-gray-400">
                Utility-first CSS with Tailwind
              </p>
            </div>

            <div className="bg-white dark:bg-gray-800 rounded-lg shadow-md p-6">
              <div className="text-purple-600 dark:text-purple-400 text-3xl mb-2">🔒</div>
              <h3 className="font-semibold text-gray-900 dark:text-white mb-2">Type-Safe</h3>
              <p className="text-sm text-gray-600 dark:text-gray-400">
                Built with TypeScript
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}

export default App
