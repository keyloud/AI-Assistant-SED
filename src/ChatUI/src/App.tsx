import { FormEvent, useEffect, useMemo, useRef, useState } from 'react'

const navItems = [
  { id: 'chats', label: 'Чаты', icon: 'chat', active: true },
  { id: 'documents', label: 'Все документы', icon: 'description', active: false },
  { id: 'archive', label: 'Архив', icon: 'inventory_2', active: false },
  { id: 'trash', label: 'Корзина', icon: 'delete', active: false },
]

const hiddenNavItems = [
  { id: 'search', label: 'Поиск в архиве', icon: 'search' },
  { id: 'insights', label: 'AI Insights', icon: 'auto_awesome' },
]

const staticRemarks = [
  {
    id: 'high',
    level: 'HIGH',
    location: 'Страница 4',
    title: 'Отсутствует подпись согласующей стороны',
    text: 'Блок подписи продавца пустой, хотя документ отмечен как завершенный.',
    color: 'text-[#ba1a1a]',
    border: 'border-[#ba1a1a]',
    badge: 'bg-[#ffdad6] text-[#93000a]',
    icon: 'error',
  },
  {
    id: 'medium',
    level: 'MEDIUM',
    location: 'Раздел 8.2',
    title: 'Неоднозначный срок прекращения договора',
    text: 'Указан notice period, но не уточнен формат календарных дней.',
    color: 'text-[#943700]',
    border: 'border-[#943700]',
    badge: 'bg-[#ffdbcd] text-[#7d2d00]',
    icon: 'warning',
  },
  {
    id: 'low',
    level: 'LOW',
    location: 'Общий',
    title: 'Несогласованный формат дат',
    text: 'Смешаны форматы MM/DD/YYYY и DD Month YYYY.',
    color: 'text-[#0c7a3e]',
    border: 'border-[#0c7a3e]',
    badge: 'bg-[#e8f7ef] text-[#0c7a3e]',
    icon: 'info',
  },
]

const steps = [
  { id: 1, name: 'OCR проверка', status: 'done', time: 'Завершено 2 мин назад' },
  { id: 2, name: 'Проверка структуры реквизитов', status: 'done', time: 'Завершено 1 мин назад' },
  { id: 3, name: 'Ожидается подтверждение пользователя', status: 'current', time: 'Текущий статус' },
]

type MessageRole = 'user' | 'assistant'

type ChatMessage = {
  id: string
  role: MessageRole
  content: string
}

type ChatApiResponse = {
  sessionId: string
  response: string
}

type ValidationApiResponse = {
  status: 'ok' | 'needs_fix' | 'template_not_found' | 'bad_request'
  documentType?: string
  extractedTextLength: number
  remarks: string[]
}

type UiRemark = {
  id: string
  level: 'HIGH' | 'MEDIUM' | 'LOW'
  location: string
  title: string
  text: string
  color: string
  border: string
  badge: string
  icon: string
}

const initialMessages: ChatMessage[] = [
  {
    id: 'assistant-seed',
    role: 'assistant',
    content:
      'Я готов к работе. Задайте вопрос по документообороту или отправьте документ для проверки обязательных реквизитов.',
  },
]

export default function App() {
  const [messages, setMessages] = useState<ChatMessage[]>(initialMessages)
  const [inputValue, setInputValue] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [isValidating, setIsValidating] = useState(false)
  const [errorText, setErrorText] = useState<string | null>(null)
  const [selectedFile, setSelectedFile] = useState<File | null>(null)
  const [documentType, setDocumentType] = useState<string | null>(null)
  const [dynamicRemarks, setDynamicRemarks] = useState<UiRemark[] | null>(null)

  const sessionIdRef = useRef(
    typeof crypto !== 'undefined' && 'randomUUID' in crypto
      ? crypto.randomUUID()
      : `session-${Date.now()}`,
  )

  const endOfMessagesRef = useRef<HTMLDivElement | null>(null)
  const fileInputRef = useRef<HTMLInputElement | null>(null)

  const conversationHistory = useMemo(
    () =>
      messages.map((m) => ({
        role: m.role,
        content: m.content,
      })),
    [messages],
  )

  useEffect(() => {
    endOfMessagesRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages, isLoading, isValidating])

  const remarksToRender = dynamicRemarks ?? staticRemarks

  function mapValidationRemarks(remarks: string[]): UiRemark[] {
    return remarks.map((text, index) => {
      const lower = text.toLowerCase()
      const isHigh = lower.includes('не найден обязательный реквизит')
      const isLow = lower.includes('ocr') || lower.includes('не удалось извлечь')

      if (isHigh) {
        return {
          id: `remark-${index}`,
          level: 'HIGH',
          location: 'Документ',
          title: 'Отсутствует обязательный реквизит',
          text,
          color: 'text-[#ba1a1a]',
          border: 'border-[#ba1a1a]',
          badge: 'bg-[#ffdad6] text-[#93000a]',
          icon: 'error',
        }
      }

      if (isLow) {
        return {
          id: `remark-${index}`,
          level: 'LOW',
          location: 'Файл',
          title: 'Техническое замечание',
          text,
          color: 'text-[#0c7a3e]',
          border: 'border-[#0c7a3e]',
          badge: 'bg-[#e8f7ef] text-[#0c7a3e]',
          icon: 'info',
        }
      }

      return {
        id: `remark-${index}`,
        level: 'MEDIUM',
        location: 'Документ',
        title: 'Замечание при проверке',
        text,
        color: 'text-[#943700]',
        border: 'border-[#943700]',
        badge: 'bg-[#ffdbcd] text-[#7d2d00]',
        icon: 'warning',
      }
    })
  }

  async function runDocumentValidation(file: File) {
    setIsValidating(true)
    setErrorText(null)

    const formData = new FormData()
    formData.append('file', file)

    const lowerName = file.name.toLowerCase()
    if (lowerName.includes('договор')) {
      formData.append('documentTypeHint', 'договор')
    } else if (lowerName.includes('довер')) {
      formData.append('documentTypeHint', 'доверенность')
    } else if (lowerName.includes('приказ')) {
      formData.append('documentTypeHint', 'приказ')
    }

    try {
      const response = await fetch('/api/documents/validate', {
        method: 'POST',
        body: formData,
      })

      const data = (await response.json()) as ValidationApiResponse

      if (!response.ok) {
        throw new Error(data.remarks?.join(' ') || `HTTP ${response.status}`)
      }

      setDocumentType(data.documentType ?? null)

      if (data.remarks.length > 0) {
        setDynamicRemarks(mapValidationRemarks(data.remarks))
      } else {
        setDynamicRemarks([
          {
            id: 'validation-ok',
            level: 'LOW',
            location: 'Документ',
            title: 'Критичных замечаний не найдено',
            text: `Проверка завершена: статус ${data.status}.`,
            color: 'text-[#0c7a3e]',
            border: 'border-[#0c7a3e]',
            badge: 'bg-[#e8f7ef] text-[#0c7a3e]',
            icon: 'check_circle',
          },
        ])
      }

      const summaryText =
        data.remarks.length === 0
          ? `Проверка файла ${file.name} завершена. Замечаний не найдено.`
          : `Проверка файла ${file.name} завершена. Найдено замечаний: ${data.remarks.length}.`

      setMessages((prev) => [
        ...prev,
        {
          id: `assistant-validation-${Date.now()}`,
          role: 'assistant',
          content: summaryText,
        },
      ])
    } catch {
      setErrorText('Не удалось выполнить проверку документа. Проверьте /api/documents/validate.')
      setMessages((prev) => [
        ...prev,
        {
          id: `assistant-validation-error-${Date.now()}`,
          role: 'assistant',
          content: 'Сервис проверки документа временно недоступен. Попробуйте снова.',
        },
      ])
    } finally {
      setIsValidating(false)
    }
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (selectedFile) {
      const currentFile = selectedFile
      setSelectedFile(null)
      setInputValue('')

      setMessages((prev) => [
        ...prev,
        {
          id: `user-file-${Date.now()}`,
          role: 'user',
          content: `Проверь документ: ${currentFile.name}`,
        },
      ])

      await runDocumentValidation(currentFile)
      return
    }

    const trimmed = inputValue.trim()
    if (!trimmed || isLoading || isValidating) {
      return
    }

    const userMessage: ChatMessage = {
      id: `user-${Date.now()}`,
      role: 'user',
      content: trimmed,
    }

    setMessages((prev) => [...prev, userMessage])
    setInputValue('')
    setIsLoading(true)
    setErrorText(null)

    try {
      const response = await fetch('/api/chat', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json; charset=utf-8',
        },
        body: JSON.stringify({
          sessionId: sessionIdRef.current,
          message: trimmed,
          conversationHistory,
        }),
      })

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`)
      }

      const data = (await response.json()) as ChatApiResponse
      const assistantText = data.response?.trim() || 'Получен пустой ответ от ассистента.'

      setMessages((prev) => [
        ...prev,
        {
          id: `assistant-${Date.now()}`,
          role: 'assistant',
          content: assistantText,
        },
      ])
    } catch {
      setErrorText('Не удалось получить ответ от backend. Проверьте доступность /api/chat.')
      setMessages((prev) => [
        ...prev,
        {
          id: `assistant-error-${Date.now()}`,
          role: 'assistant',
          content: 'Сервис временно недоступен. Попробуйте отправить запрос еще раз.',
        },
      ])
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <div className="h-screen overflow-hidden bg-[#f3f6fa] text-[#1a1d22]">
      <div className="flex h-full">
        <aside className="hidden w-72 shrink-0 flex-col border-r border-[#dce3ee] bg-[#eef3fa] px-4 py-5 lg:flex">
          <div className="mb-8 px-2">
            <h1 className="font-['Manrope'] text-xl font-extrabold tracking-tight text-[#111827]">AI-Assistant</h1>
            <p className="font-['Inter'] text-xs text-[#64748b]">Готов к анализу</p>
          </div>

          <nav className="space-y-1">
            {navItems.map((item) => (
              <button
                key={item.id}
                type="button"
                className={`flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-left font-['Inter'] text-sm transition-colors ${
                  item.active
                    ? 'bg-white font-semibold text-[#1d4ed8] shadow-sm'
                    : 'text-[#475569] hover:bg-white/70'
                }`}
              >
                <span className="material-symbols-outlined text-[20px]">{item.icon}</span>
                <span>{item.label}</span>
              </button>
            ))}

            {hiddenNavItems.map((item) => (
              <button
                key={item.id}
                type="button"
                className="hidden w-full items-center gap-3 rounded-xl px-3 py-2.5 text-left font-['Inter'] text-sm text-[#475569]"
              >
                <span className="material-symbols-outlined text-[20px]">{item.icon}</span>
                <span>{item.label}</span>
              </button>
            ))}
          </nav>

          <div className="mt-auto space-y-4 pt-4">
            <div className="rounded-2xl bg-white p-4 shadow-sm">
              <div className="mb-2 flex items-center justify-between">
                <span className="font-['Inter'] text-[10px] font-bold uppercase tracking-[0.14em] text-[#64748b]">
                  Статус документа
                </span>
                <span className="rounded-full bg-[#e8f7ef] px-2 py-0.5 text-[10px] font-bold text-[#0c7a3e]">Проверен</span>
              </div>

              <div className="flex items-center gap-3">
                <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-[#eef3fa]">
                  <span className="material-symbols-outlined text-[#1d4ed8]">article</span>
                </div>

                <div>
                  <p className="max-w-[140px] truncate font-['Manrope'] text-xs font-bold text-[#0f172a]">
                    {selectedFile?.name ?? 'Документ не выбран'}
                  </p>
                  <p className="font-['Inter'] text-[10px] text-[#64748b]">
                    {documentType ? `Тип: ${documentType}` : 'Ожидает проверки'}
                  </p>
                </div>
              </div>
            </div>

            <button
              type="button"
              className="w-full rounded-xl bg-gradient-to-br from-[#1d4ed8] to-[#2563eb] px-4 py-3 font-['Manrope'] text-sm font-bold text-white shadow-lg shadow-[#1d4ed8]/25"
            >
              + Новый анализ
            </button>
          </div>
        </aside>

        <main className="flex min-w-0 flex-1 flex-col">
          <header className="flex items-center justify-between border-b border-[#dce3ee] bg-[#f8fbff] px-6 py-4 xl:px-8">
            <div>
              <h2 className="font-['Manrope'] text-xl font-bold tracking-tight text-[#111827]">Чаты</h2>
              <p className="font-['Inter'] text-xs text-[#64748b]">Проверка документов и рекомендации</p>
            </div>

            <div className="flex items-center gap-3">
              <button type="button" className="rounded-full p-2 text-[#64748b] hover:bg-white">
                <span className="material-symbols-outlined">notifications</span>
              </button>
              <button type="button" className="rounded-full p-2 text-[#64748b] hover:bg-white">
                <span className="material-symbols-outlined">settings</span>
              </button>
              <div className="h-9 w-9 rounded-full bg-[#dbeafe]" />
            </div>
          </header>

          <div className="flex min-h-0 flex-1">
            <section className="flex min-w-0 flex-1 flex-col overflow-hidden px-4 pb-4 pt-4 md:px-6 xl:px-8">
              <div className="mx-auto w-full max-w-4xl space-y-4 overflow-y-auto pr-1">
                {messages.map((message) =>
                  message.role === 'assistant' ? (
                    <div key={message.id} className="flex items-start gap-4">
                      <div className="mt-1 flex h-8 w-8 items-center justify-center rounded-full bg-[#1d4ed8] text-white">
                        <span className="material-symbols-outlined text-base">auto_awesome</span>
                      </div>

                      <article className="max-w-[92%] rounded-2xl rounded-tl-none border-l-4 border-[#1d4ed8] bg-white p-5 shadow-sm">
                        <p className="whitespace-pre-wrap font-['Inter'] text-sm leading-relaxed text-[#334155]">{message.content}</p>
                      </article>
                    </div>
                  ) : (
                    <div key={message.id} className="flex items-start justify-end gap-3">
                      <div className="max-w-[70%] rounded-2xl rounded-tr-none bg-[#1d4ed8] px-4 py-3 text-white shadow-sm">
                        <p className="whitespace-pre-wrap font-['Inter'] text-sm">{message.content}</p>
                      </div>
                      <div className="h-8 w-8 rounded-full bg-[#dbeafe]" />
                    </div>
                  ),
                )}

                {isLoading && (
                  <div className="flex items-start gap-4">
                    <div className="mt-1 flex h-8 w-8 items-center justify-center rounded-full bg-[#1d4ed8] text-white">
                      <span className="material-symbols-outlined text-base">auto_awesome</span>
                    </div>
                    <article className="max-w-[92%] rounded-2xl rounded-tl-none border-l-4 border-[#1d4ed8] bg-white p-5 shadow-sm">
                      <p className="font-['Inter'] text-sm text-[#64748b]">Ассистент формирует ответ...</p>
                    </article>
                  </div>
                )}

                {errorText && (
                  <div className="rounded-xl border border-[#fecaca] bg-[#fff1f1] px-4 py-3 font-['Inter'] text-sm text-[#991b1b]">
                    {errorText}
                  </div>
                )}

                <div ref={endOfMessagesRef} />
              </div>

              <div className="mx-auto mt-6 w-full max-w-4xl">
                <form onSubmit={handleSubmit} className="flex items-center gap-2 rounded-2xl border border-[#dce3ee] bg-white p-2 shadow-sm">
                  <input
                    ref={fileInputRef}
                    type="file"
                    accept=".pdf,.docx"
                    className="hidden"
                    onChange={(e) => {
                      const file = e.target.files?.[0] ?? null
                      setSelectedFile(file)
                      setErrorText(null)
                    }}
                  />
                  <button
                    type="button"
                    className="rounded-xl p-2 text-[#64748b] hover:bg-[#f3f6fa]"
                    title="Прикрепить PDF или DOCX"
                    onClick={() => fileInputRef.current?.click()}
                    disabled={isLoading || isValidating}
                  >
                    <span className="material-symbols-outlined">attach_file</span>
                  </button>
                  {selectedFile && (
                    <span className="max-w-[180px] truncate rounded-lg bg-[#e9f0ff] px-2 py-1 font-['Inter'] text-xs text-[#1d4ed8]">
                      {selectedFile.name}
                    </span>
                  )}
                  <input
                    type="text"
                    value={inputValue}
                    onChange={(e) => setInputValue(e.target.value)}
                    placeholder={selectedFile ? 'Нажмите отправку для проверки документа...' : 'Задайте вопрос или отправьте комментарий по документу...'}
                    className="w-full border-none bg-transparent px-1 py-2 font-['Inter'] text-sm text-[#0f172a] placeholder:text-[#94a3b8] focus:outline-none"
                    disabled={isLoading || isValidating}
                  />
                  <button
                    type="submit"
                    className="rounded-xl bg-[#1d4ed8] p-3 text-white disabled:cursor-not-allowed disabled:opacity-50"
                    disabled={isLoading || isValidating || (!inputValue.trim() && !selectedFile)}
                  >
                    <span className="material-symbols-outlined">send</span>
                  </button>
                </form>
              </div>
            </section>

            <aside className="hidden w-80 shrink-0 border-l border-[#dce3ee] bg-[#eef3fa] p-6 xl:block">
              <div className="mb-4 flex items-center justify-between">
                <h3 className="font-['Manrope'] text-lg font-bold text-[#0f172a]">Замечания анализа</h3>
                <button type="button" className="rounded-full p-1 text-[#64748b] hover:bg-white">
                  <span className="material-symbols-outlined">filter_list</span>
                </button>
              </div>

              <div className="space-y-3">
                {remarksToRender.map((item) => (
                  <article key={item.id} className={`rounded-2xl border-l-4 ${item.border} bg-white p-4 shadow-sm`}>
                    <div className="mb-1 flex items-center gap-2">
                      <span className={`material-symbols-outlined text-[18px] ${item.color}`}>{item.icon}</span>
                      <span className={`rounded-full px-2 py-0.5 text-[10px] font-bold uppercase tracking-[0.12em] ${item.badge}`}>
                        {item.level}
                      </span>
                      <span className="font-['Inter'] text-[10px] text-[#64748b]">{item.location}</span>
                    </div>
                    <h4 className="font-['Inter'] text-sm font-semibold text-[#0f172a]">{item.title}</h4>
                    <p className="mt-1 font-['Inter'] text-xs leading-relaxed text-[#475569]">{item.text}</p>
                  </article>
                ))}
              </div>

              <div className="mt-6 border-t border-[#dce3ee] pt-5">
                <h4 className="mb-4 font-['Inter'] text-[10px] font-bold uppercase tracking-[0.14em] text-[#64748b]">
                  Этапы проверки
                </h4>

                <ol className="space-y-4">
                  {steps.map((step) => (
                    <li key={step.id} className="flex items-start gap-3">
                      {step.status === 'done' ? (
                        <span className="mt-0.5 flex h-4 w-4 items-center justify-center rounded-full bg-[#0c7a3e] text-white">
                          <span className="material-symbols-outlined text-[12px]">check</span>
                        </span>
                      ) : (
                        <span className="mt-0.5 h-4 w-4 rounded-full border-4 border-[#bfdbfe] bg-[#1d4ed8]" />
                      )}

                      <div>
                        <p className={`font-['Inter'] text-xs font-semibold ${step.status === 'current' ? 'text-[#1d4ed8]' : 'text-[#0f172a]'}`}>
                          {step.name}
                        </p>
                        <p className="font-['Inter'] text-[10px] text-[#64748b]">{step.time}</p>
                      </div>
                    </li>
                  ))}
                </ol>
              </div>
            </aside>
          </div>
        </main>
      </div>
    </div>
  )
}
