export default function App() {
  return (
    <div className="flex h-screen bg-gray-100 text-gray-900">

      {/* Left panel: Document context */}
      <aside className="w-80 bg-white border-r border-gray-200 flex flex-col shrink-0">
        <div className="p-4 border-b border-gray-200">
          <h2 className="text-sm font-semibold text-gray-500 uppercase tracking-wide">
            Контекст документа
          </h2>
        </div>
        <div className="flex-1 flex items-center justify-center p-6 text-center">
          <p className="text-sm text-gray-400">
            Выберите документ для отображения контекста бизнес-процесса
          </p>
        </div>
      </aside>

      {/* Right panel: Chat */}
      <main className="flex-1 flex flex-col min-w-0">
        <header className="bg-white border-b border-gray-200 px-6 py-4 shrink-0">
          <h1 className="text-lg font-bold text-gray-800">AI Ассистент СЭД</h1>
          <p className="text-xs text-gray-400 mt-0.5">
            Интеллектуальный помощник по документообороту
          </p>
        </header>

        {/* Messages area */}
        <div className="flex-1 flex items-center justify-center">
          <div className="text-center space-y-2">
            <div className="w-12 h-12 rounded-full bg-blue-50 flex items-center justify-center mx-auto">
              <span className="text-2xl">🤖</span>
            </div>
            <p className="text-sm font-medium text-gray-600">Чат готов к работе</p>
            <p className="text-xs text-gray-400">Подключение к LLM — Фаза 2</p>
          </div>
        </div>

        {/* Input */}
        <div className="bg-white border-t border-gray-200 px-4 py-3 shrink-0">
          <div className="flex gap-2 max-w-4xl mx-auto">
            <input
              type="text"
              placeholder="Введите вопрос по документообороту..."
              className="flex-1 border border-gray-300 rounded-lg px-4 py-2 text-sm bg-gray-50 text-gray-400 cursor-not-allowed focus:outline-none"
            />
            <button
              disabled
              className="bg-blue-500 text-white px-5 py-2 rounded-lg text-sm font-medium opacity-40 cursor-not-allowed"
            >
              Отправить
            </button>
          </div>
        </div>
      </main>

    </div>
  )
}
