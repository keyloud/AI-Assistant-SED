import { useState, useRef, useEffect } from 'react'

interface Message {
  id: string
  role: 'user' | 'assistant'
  content: string
  isValidation?: boolean
  fileName?: string
}

interface ChatResponse {
  sessionId: string
  response: string
  requestType: string
}

interface ValidateDocumentResponse {
  fileName: string
  validationResult: {
    validationSummary: string
  }
}

const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? 'http://localhost:5000'

export default function App() {
  const [messages, setMessages] = useState<Message[]>([])
  const [inputText, setInputText] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [sessionId] = useState(() => crypto.randomUUID())
  const [uploadedFile, setUploadedFile] = useState<File | null>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const messagesEndRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages])

  const addMessage = (msg: Omit<Message, 'id'>) => {
    setMessages(prev => [...prev, { ...msg, id: crypto.randomUUID() }])
  }

  const handleSend = async () => {
    if (isLoading) return

    if (uploadedFile) {
      await handleValidateDocument()
      return
    }

    if (!inputText.trim()) return

    const userText = inputText.trim()
    setInputText('')
    addMessage({ role: 'user', content: userText })
    setIsLoading(true)

    try {
      const res = await fetch(`${API_BASE}/api/chat`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          sessionId,
          message: userText,
          userId: 'user-001',
          conversationHistory: []
        })
      })

      if (!res.ok) throw new Error(`HTTP ${res.status}`)

      const data = await res.json() as ChatResponse
      addMessage({ role: 'assistant', content: data.response })
    } catch (err) {
      console.error('Chat request failed:', err)
      addMessage({
        role: 'assistant',
        content: 'Ошибка соединения с сервером. Убедитесь, что бэкенд запущен.'
      })
    } finally {
      setIsLoading(false)
    }
  }

  const handleValidateDocument = async () => {
    if (!uploadedFile) return

    const file = uploadedFile
    const comment = inputText.trim()
    setUploadedFile(null)
    setInputText('')

    addMessage({
      role: 'user',
      content: `Документ для проверки: ${file.name}${comment ? `\n${comment}` : ''}`,
      fileName: file.name
    })
    setIsLoading(true)

    try {
      const formData = new FormData()
      formData.append('file', file)
      formData.append('sessionId', sessionId)

      const res = await fetch(`${API_BASE}/api/document/validate`, {
        method: 'POST',
        body: formData
      })

      if (!res.ok) {
        const err = await res.json().catch(() => ({ error: `HTTP ${res.status}` })) as { error?: string }
        throw new Error(err.error ?? `HTTP ${res.status}`)
      }

      const data = await res.json() as ValidateDocumentResponse
      addMessage({
        role: 'assistant',
        content: data.validationResult.validationSummary,
        isValidation: true,
        fileName: data.fileName
      })
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Неизвестная ошибка'
      addMessage({ role: 'assistant', content: `Ошибка проверки документа: ${message}` })
    } finally {
      setIsLoading(false)
    }
  }

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (file) setUploadedFile(file)
    e.target.value = ''
  }

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      void handleSend()
    }
  }

  return (
    <div className="flex h-screen bg-gray-100 text-gray-900">

      {/* Left panel: Document context */}
      <aside className="w-80 bg-white border-r border-gray-200 flex flex-col shrink-0">
        <div className="p-4 border-b border-gray-200">
          <h2 className="text-sm font-semibold text-gray-500 uppercase tracking-wide">
            Контекст документа
          </h2>
        </div>
        <div className="flex-1 overflow-y-auto p-4">
          {uploadedFile ? (
            <div className="bg-blue-50 rounded-lg p-3 text-sm">
              <div className="flex items-start gap-2 mb-1">
                <span className="text-blue-600 mt-0.5">📄</span>
                <span className="font-medium text-blue-800 break-all">{uploadedFile.name}</span>
              </div>
              <p className="text-blue-500 text-xs ml-6">{(uploadedFile.size / 1024).toFixed(1)} КБ</p>
              <button
                onClick={() => setUploadedFile(null)}
                className="mt-2 ml-6 text-xs text-red-400 hover:text-red-600 transition-colors"
              >
                Удалить
              </button>
            </div>
          ) : (
            <div className="text-center mt-8 space-y-2">
              <p className="text-sm text-gray-400">
                Прикрепите документ (.txt, .docx) для проверки его заполнения
              </p>
              <p className="text-xs text-gray-300">
                или задайте вопрос по документообороту
              </p>
            </div>
          )}
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
        <div className="flex-1 overflow-y-auto px-4 py-4 space-y-4">
          {messages.length === 0 ? (
            <div className="flex items-center justify-center h-full">
              <div className="text-center space-y-2">
                <div className="w-12 h-12 rounded-full bg-blue-50 flex items-center justify-center mx-auto">
                  <span className="text-2xl">🤖</span>
                </div>
                <p className="text-sm font-medium text-gray-600">Чат готов к работе</p>
                <p className="text-xs text-gray-400">
                  Задайте вопрос или прикрепите документ для проверки
                </p>
              </div>
            </div>
          ) : (
            messages.map(msg => (
              <div key={msg.id} className={`flex ${msg.role === 'user' ? 'justify-end' : 'justify-start'}`}>
                <div className={`max-w-[80%] rounded-2xl px-4 py-3 text-sm ${
                  msg.role === 'user'
                    ? 'bg-blue-500 text-white rounded-br-none'
                    : msg.isValidation
                    ? 'bg-green-50 border border-green-200 text-gray-800 rounded-bl-none'
                    : 'bg-white border border-gray-200 text-gray-800 rounded-bl-none'
                }`}>
                  {msg.isValidation && (
                    <div className="flex items-center gap-1 mb-2 text-green-700 font-medium text-xs">
                      <span>✅</span>
                      <span>Результаты проверки: {msg.fileName}</span>
                    </div>
                  )}
                  <p className="whitespace-pre-wrap leading-relaxed">{msg.content}</p>
                </div>
              </div>
            ))
          )}

          {isLoading && (
            <div className="flex justify-start">
              <div className="bg-white border border-gray-200 rounded-2xl rounded-bl-none px-4 py-3">
                <div className="flex gap-1 items-center">
                  <div className="w-2 h-2 bg-gray-400 rounded-full animate-bounce" style={{ animationDelay: '0ms' }} />
                  <div className="w-2 h-2 bg-gray-400 rounded-full animate-bounce" style={{ animationDelay: '150ms' }} />
                  <div className="w-2 h-2 bg-gray-400 rounded-full animate-bounce" style={{ animationDelay: '300ms' }} />
                </div>
              </div>
            </div>
          )}
          <div ref={messagesEndRef} />
        </div>

        {/* Input area */}
        <div className="bg-white border-t border-gray-200 px-4 py-3 shrink-0">
          {uploadedFile && (
            <div className="flex items-center gap-2 mb-2 px-1">
              <span className="text-xs text-blue-600 bg-blue-50 rounded-full px-3 py-1 flex items-center gap-1">
                📄 {uploadedFile.name}
                <button
                  onClick={() => setUploadedFile(null)}
                  className="ml-1 text-blue-400 hover:text-blue-600 font-bold leading-none"
                  aria-label="Удалить файл"
                >
                  ×
                </button>
              </span>
            </div>
          )}
          <div className="flex gap-2 max-w-4xl mx-auto">
            <input
              type="file"
              ref={fileInputRef}
              onChange={handleFileSelect}
              accept=".txt,.docx"
              className="hidden"
              aria-label="Загрузить документ"
            />
            <button
              onClick={() => fileInputRef.current?.click()}
              disabled={isLoading}
              className="flex-shrink-0 w-10 h-10 flex items-center justify-center border border-gray-300 rounded-lg text-gray-500 hover:text-gray-700 hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
              title="Прикрепить документ (.txt, .docx)"
              aria-label="Прикрепить документ"
            >
              📎
            </button>
            <input
              type="text"
              value={inputText}
              onChange={e => setInputText(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder={
                uploadedFile
                  ? 'Добавьте комментарий к документу (необязательно)...'
                  : 'Введите вопрос по документообороту...'
              }
              disabled={isLoading}
              className="flex-1 border border-gray-300 rounded-lg px-4 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:bg-gray-50 disabled:text-gray-400 transition-colors"
            />
            <button
              onClick={() => void handleSend()}
              disabled={isLoading || (!inputText.trim() && !uploadedFile)}
              className="bg-blue-500 text-white px-5 py-2 rounded-lg text-sm font-medium hover:bg-blue-600 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
            >
              {uploadedFile ? 'Проверить' : 'Отправить'}
            </button>
          </div>
        </div>
      </main>

    </div>
  )
}
