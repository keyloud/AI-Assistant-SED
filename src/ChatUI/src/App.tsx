import { useState, useRef, useEffect, useMemo, FormEvent } from 'react'
import { renderAsync } from 'docx-preview'
import { toPng } from 'html-to-image'

const navItems = [
  { id: 'documents', label: 'Все документы', icon: 'description', active: true },
  { id: 'archive', label: 'Архив', icon: 'inventory_2', active: false },
  { id: 'trash', label: 'Корзина', icon: 'delete', active: false },
]

const hiddenNavItems = [
  { id: 'search', label: 'Поиск в архиве', icon: 'search' },
  { id: 'insights', label: 'AI Insights', icon: 'auto_awesome' },
]

type MessageRole = 'user' | 'assistant'

type ChatMessage = {
  id: string
  role: MessageRole
  content: string
  ragSources?: RagSource[]
}

type RagSource = {
  title: string
  sourceFile?: string
  section: string
  score: number
}

type ChatApiResponse = {
  sessionId: string
  chatTitle?: string
  updatedAt?: string
  response: string
  ragSources?: RagSource[]
}

type ChatSessionApiResponse = {
  sessionId: string
  title: string
  updatedAt: string
  status: ChatStatus
  documents: {
    id: string
    name: string
    uploadDate: string
    status: DocumentStatus
    size: string
    uploadedBy: string
  }[]
  messages?: {
    role: MessageRole
    content: string
    timestamp: string
  }[]
}

type ValidationApiResponse = {
  status: 'ok' | 'needs_fix' | 'template_not_found' | 'bad_request'
  documentType?: string
  classificationConfidence?: number
  ocrUsed?: boolean
  extractedTextLength: number
  extractedText?: string
  summary?: string
  recommendations?: string[]
  remarks?: string[]
}

type DocumentStatus = 'checked' | 'processing' | 'error'

type DocumentItem = {
  id: string
  name: string
  uploadDate: string
  status: DocumentStatus
  size: string
  uploadedBy: string
}

type ChatStatus = 'active' | 'archive' | 'error'

type ChatItem = {
  id: string
  title: string
  documents: DocumentItem[]
  updatedAt: string
  status: ChatStatus
}

type PendingFilePreview = {
  name: string
  sizeLabel: string
  kindLabel: string
  previewKind: 'pdf' | 'image'
  previewUrl: string | null
}

const initialMessages: ChatMessage[] = [
  {
    id: 'assistant-seed',
    role: 'assistant',
    content:
      'Я готов к работе. Задайте вопрос по документообороту или отправьте документ для проверки обязательных реквизитов.',
  },
]

const mockDocuments: DocumentItem[] = [
  {
    id: 'doc-1',
    name: 'Q3_Financial_Report_Final.pdf',
    uploadDate: '12 Окт 2023, 14:30',
    status: 'checked',
    size: '2.4 MB',
    uploadedBy: 'Анна Смирнова',
  },
  {
    id: 'doc-2',
    name: 'Legal_Contract_V2_Draft.docx',
    uploadDate: '12 Окт 2023, 11:15',
    status: 'processing',
    size: '1.1 MB',
    uploadedBy: 'Иван Петров',
  },
  {
    id: 'doc-3',
    name: 'Scanned_Invoice_0899.png',
    uploadDate: '11 Окт 2023, 09:45',
    status: 'error',
    size: '4.8 MB',
    uploadedBy: 'Мария Козлова',
  },
]

const mockChats: ChatItem[] = [
  {
    id: 'chat-1',
    title: 'Review of Purchase Agreement',
    updatedAt: 'Сегодня, 14:30',
    status: 'active',
    documents: [
      {
        id: 'chat-1-doc-1',
        name: 'PA_Final_v3.pdf',
        uploadDate: '19 Апр 2026, 14:10',
        status: 'checked',
        size: '2.1 MB',
        uploadedBy: 'Анна Смирнова',
      },
      {
        id: 'chat-1-doc-2',
        name: 'PA_Appendix_A.docx',
        uploadDate: '19 Апр 2026, 14:12',
        status: 'checked',
        size: '0.9 MB',
        uploadedBy: 'Анна Смирнова',
      },
      {
        id: 'chat-1-doc-3',
        name: 'Pricing_Table_2026.xlsx',
        uploadDate: '19 Апр 2026, 14:13',
        status: 'processing',
        size: '1.3 MB',
        uploadedBy: 'Анна Смирнова',
      },
    ],
  },
  {
    id: 'chat-2',
    title: 'General Inquiry about Policies',
    updatedAt: 'Вчера, 09:15',
    status: 'archive',
    documents: [],
  },
  {
    id: 'chat-3',
    title: 'Q4 Financial Report Analysis',
    updatedAt: '12 Окт, 16:45',
    status: 'error',
    documents: [mockDocuments[0]],
  },
  {
    id: 'chat-4',
    title: 'Employee Onboarding Draft',
    updatedAt: '10 Окт, 11:20',
    status: 'active',
    documents: [mockDocuments[1]],
  },
]

function mapApiChatToUi(item: ChatSessionApiResponse): ChatItem {
  return {
    id: item.sessionId,
    title: item.title,
    updatedAt: item.updatedAt,
    status: item.status,
    documents: item.documents.map((doc) => ({
      id: doc.id,
      name: doc.name,
      uploadDate: doc.uploadDate,
      status: doc.status,
      size: doc.size,
      uploadedBy: doc.uploadedBy,
    })),
  }
}

function getDocumentIcon(name: string): string {
  const lower = name.toLowerCase()
  if (lower.endsWith('.pdf')) return 'picture_as_pdf'
  if (lower.endsWith('.docx') || lower.endsWith('.doc') || lower.endsWith('.txt')) return 'description'
  if (lower.endsWith('.png') || lower.endsWith('.jpg') || lower.endsWith('.jpeg')) return 'image'
  return 'description'
}

function resolveDocumentTypeHint(fileName: string): string | undefined {
  const lower = fileName.toLowerCase()
  if (lower.includes('приказ')) return 'приказ'
  if (lower.includes('довер')) return 'доверенность'
  if (lower.includes('договор')) return 'договор'
  return undefined
}

async function fetchWithTimeout(input: RequestInfo | URL, init: RequestInit, timeoutMs: number): Promise<Response> {
  const controller = new AbortController()
  const timeoutId = setTimeout(() => controller.abort(), timeoutMs)

  try {
    return await fetch(input, {
      ...init,
      signal: controller.signal,
    })
  } finally {
    clearTimeout(timeoutId)
  }
}

function parseMarkdownFormatting(text: string): (string | React.ReactElement)[] {
  const result: (string | React.ReactElement)[] = []
  let lastIndex = 0
  let keyCounter = 0

  // Регулярное выражение для поиска **текст**, *текст*, _текст_
  // Используем более сложное выражение для правильного парсинга
  const regex = /\*\*(.+?)\*\*|\*([^\*]+?)\*|_([^_]+?)_/g
  let match

  while ((match = regex.exec(text)) !== null) {
    // Добавляем текст перед этим совпадением
    if (match.index > lastIndex) {
      result.push(text.slice(lastIndex, match.index))
    }

    // Добавляем форматированный элемент
    if (match[1]) {
      // **текст** - жирный
      result.push(
        <strong key={`strong-${keyCounter++}`} className="font-semibold">
          {match[1]}
        </strong>,
      )
    } else if (match[2]) {
      // *текст* - курсив
      result.push(
        <em key={`em1-${keyCounter++}`} className="italic">
          {match[2]}
        </em>,
      )
    } else if (match[3]) {
      // _текст_ - курсив
      result.push(
        <em key={`em2-${keyCounter++}`} className="italic">
          {match[3]}
        </em>,
      )
    }

    lastIndex = match.index + match[0].length
  }

  // Добавляем оставшийся текст
  if (lastIndex < text.length) {
    result.push(text.slice(lastIndex))
  }

  return result.length === 0 ? [text] : result
}

function getStatusBadge(status: DocumentStatus): {
  badge: string
  icon: string
  label: string
} {
  switch (status) {
    case 'checked':
      return {
        badge: 'bg-[#ffdbcd] text-[#7d2d00]',
        icon: 'check_circle',
        label: 'Проверено',
      }
    case 'processing':
      return {
        badge: 'bg-[#dbe1ff] text-[#003ea8]',
        icon: 'schedule',
        label: 'В процессе',
      }
    case 'error':
      return {
        badge: 'bg-[#ffdad6] text-[#93000a]',
        icon: 'error',
        label: 'Ошибка',
      }
  }
}

function getChatStatusBadge(status: ChatStatus): {
  badge: string
  icon?: string
  label: string
} {
  switch (status) {
    case 'active':
      return {
        badge: 'bg-[#ffdbcd] text-[#7d2d00]',
        label: 'Активен',
      }
    case 'archive':
      return {
        badge: 'bg-[#e6e8ea] text-[#434655]',
        label: 'Архив',
      }
    case 'error':
      return {
        badge: 'bg-[#ffdad6] text-[#93000a]',
        icon: 'error',
        label: 'Ошибка анализа',
      }
  }
}

function DocumentsTable({
  chats,
  onOpenChat,
  onDeleteChat,
}: {
  chats: ChatItem[]
  onOpenChat: (chat: ChatItem) => void
  onDeleteChat: (chatId: string) => void
}) {
  return (
    <div className="rounded-2xl bg-white shadow-sm overflow-hidden border border-[#dce3ee] flex h-full min-h-0 flex-col">
      <div className="min-h-0 flex-1 overflow-y-auto">
        <table className="w-full text-left border-collapse">
          <thead>
            <tr className="border-b border-[#dce3ee] text-xs font-medium text-[#64748b] uppercase tracking-wider sticky top-0 z-10 bg-white">
              <th className="py-5 px-6">Название чата</th>
              <th className="py-5 px-6">Документ</th>
              <th className="py-5 px-6">Дата обновления</th>
              <th className="py-5 px-6">Статус</th>
              <th className="py-5 px-6 text-right">Действия</th>
            </tr>
          </thead>
          <tbody className="text-sm divide-y divide-[#dce3ee]">
            {chats.map((chat) => {
            const statusInfo = getChatStatusBadge(chat.status)
            const hasDocuments = chat.documents.length > 0
            const firstDocument = chat.documents[0]
            const extraDocumentsCount = Math.max(0, chat.documents.length - 1)
            const icon = hasDocuments ? getDocumentIcon(firstDocument.name) : 'chat'

            return (
              <tr 
                key={chat.id} 
                className="hover:bg-[#f3f6fa] transition-colors cursor-pointer"
                onClick={() => onOpenChat(chat)}
              >
                <td className="py-4 px-6">
                  <div className="flex items-center gap-4">
                    <div
                      className={`w-10 h-10 rounded-full flex items-center justify-center ${
                        hasDocuments ? 'bg-[#dbe1ff] text-[#0053db]' : 'bg-[#e6e8ea] text-[#737686]'
                      }`}
                    >
                      <span className="material-symbols-outlined text-base" data-icon={icon}>
                        {icon}
                      </span>
                    </div>
                    <div className="font-medium text-[#1a1d22]">{chat.title}</div>
                  </div>
                </td>

                <td className="py-4 px-6 text-[#64748b]">
                  {hasDocuments ? (
                    <div className="flex items-center gap-2">
                      <span className="material-symbols-outlined text-[18px]">{getDocumentIcon(firstDocument.name)}</span>
                      <span className="truncate max-w-[220px]">{firstDocument.name}</span>
                      {extraDocumentsCount > 0 && (
                        <span className="inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium bg-[#dbe1ff] text-[#003ea8]">
                          +{extraDocumentsCount} файла
                        </span>
                      )}
                    </div>
                  ) : (
                    <span className="text-[#737686]">-</span>
                  )}
                </td>

                <td className="py-4 px-6 text-[#64748b]">{chat.updatedAt}</td>

                <td className="py-4 px-6">
                  <span className={`inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-medium ${statusInfo.badge}`}>
                    {statusInfo.icon && (
                      <span className="material-symbols-outlined text-sm" data-icon={statusInfo.icon}>
                        {statusInfo.icon}
                      </span>
                    )}
                    {statusInfo.label}
                  </span>
                </td>

                <td className="py-4 px-6 text-right">
                  <div className="flex items-center justify-end gap-2">
                    <button
                      onClick={(e) => {
                        e.stopPropagation()
                        onDeleteChat(chat.id)
                      }}
                      className="inline-flex items-center justify-center rounded-lg px-3 py-1.5 text-[#93000a] hover:bg-[#fff1f1] transition-colors"
                      title="Удалить чат"
                    >
                      <span className="material-symbols-outlined text-[18px]">delete</span>
                    </button>
                  </div>
                </td>
              </tr>
            )
          })}
          </tbody>
        </table>
      </div>
    </div>
  )
}

function DocumentsView({
  documents,
  onOpenDocument,
  onDeleteChat,
}: {
  documents: ChatItem[]
  onOpenDocument: (chat: ChatItem) => void
  onDeleteChat: (chatId: string) => void
}) {
  const [searchQuery, setSearchQuery] = useState('')

  const filteredChats = documents.filter(
    (chat) =>
      chat.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
      chat.documents.some((doc) => doc.name.toLowerCase().includes(searchQuery.toLowerCase())),
  )

  return (
    <main className="flex min-w-0 flex-1 flex-col gap-6 p-6 min-h-0">
      <header className="shrink-0">
        <h1 className="font-['Manrope'] text-3xl font-extrabold tracking-tight text-[#111827] mb-6">
          Все документы
        </h1>

        <div className="flex flex-col md:flex-row gap-4 items-start md:items-center justify-between">
          <div className="relative w-full md:w-96">
            <input
              type="text"
              placeholder="Поиск по чатам или документам..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="w-full px-4 py-3 rounded-lg border border-[#dce3ee] bg-white text-[#1a1d22] placeholder-[#64748b] focus:outline-none focus:ring-2 focus:ring-[#0053db] focus:border-transparent"
            />
            <span className="material-symbols-outlined absolute right-3 top-3 text-[#64748b] pointer-events-none">
              search
            </span>
          </div>

          <div className="flex gap-3">
            <button className="flex items-center gap-2 px-4 py-2.5 rounded-lg bg-white border border-[#dce3ee] text-[#1a1d22] hover:bg-[#f3f6fa] transition-colors font-medium text-sm">
              <span className="material-symbols-outlined text-base" data-icon="filter_list">
                filter_list
              </span>
              Статус
            </button>
            <button className="flex items-center gap-2 px-4 py-2.5 rounded-lg bg-white border border-[#dce3ee] text-[#1a1d22] hover:bg-[#f3f6fa] transition-colors font-medium text-sm">
              <span className="material-symbols-outlined text-base" data-icon="calendar_today">
                calendar_today
              </span>
              Дата
            </button>
          </div>
        </div>
      </header>

      <div className="flex min-h-0 flex-1 flex-col">
        <DocumentsTable chats={filteredChats} onOpenChat={onOpenDocument} onDeleteChat={onDeleteChat} />

        <div className="shrink-0 flex items-center justify-between pt-4 px-2">
          <span className="text-sm text-[#64748b] font-medium">
            Показано {filteredChats.length} из {documents.length} чатов
          </span>
        </div>
      </div>
    </main>
  )
}

function DocumentDetailsView({
  document,
  chatTitle,
  onBack,
  messages,
  inputValue,
  isLoading,
  errorText,
  selectedFile,
  documentPreview,
  attachmentLimitReached,
  onInputChange,
  onFileSelect,
  onSubmit,
}: {
  document: DocumentItem | null
  chatTitle: string
  onBack: () => void
  messages: ChatMessage[]
  inputValue: string
  isLoading: boolean
  errorText: string | null
  selectedFile: File | null
  documentPreview: PendingFilePreview | null
  attachmentLimitReached: boolean
  onInputChange: (value: string) => void
  onFileSelect: (file: File | null) => void
  onSubmit: (e: FormEvent<HTMLFormElement>) => void
}) {
  const fileInputRef = useRef<HTMLInputElement | null>(null)
  const endOfMessagesRef = useRef<HTMLDivElement | null>(null)

  useEffect(() => {
    endOfMessagesRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages, isLoading])

  const statusInfo = document ? getStatusBadge(document.status) : null

  return (
    <main className="flex min-w-0 flex-1 flex-col h-full overflow-hidden p-6 gap-6">
      {/* Header */}
      <header className="flex items-center justify-between shrink-0">
        <div className="flex items-center gap-4">
          <button
            onClick={onBack}
            className="flex items-center gap-2 text-[#64748b] hover:text-[#0053db] transition-colors py-2 px-3 rounded-lg hover:bg-[#f3f6fa] group"
          >
            <span className="material-symbols-outlined text-[20px] group-hover:-translate-x-1 transition-transform" data-icon="arrow_back">
              arrow_back
            </span>
            <span className="font-['Inter'] text-[0.875rem] font-medium">Назад к списку</span>
          </button>
          <div className="w-px h-6 bg-[#dce3ee]/30"></div>
          <h1 className="font-['Manrope'] text-[1.375rem] font-bold tracking-tight">Детали чата: {chatTitle}</h1>
        </div>
        <div className="flex items-center gap-3">
          <button className="flex items-center gap-2 bg-white text-[#1a1d22] hover:bg-[#f3f6fa] transition-colors py-2 px-4 rounded-lg text-[0.875rem] font-medium shadow-sm border border-[#dce3ee]">
            <span className="material-symbols-outlined text-[18px]" data-icon="share">
              share
            </span>
            Поделиться
          </button>
          <button className="flex items-center gap-2 bg-[#0053db] text-white hover:opacity-90 transition-opacity py-2 px-5 rounded-lg text-[0.875rem] font-medium shadow-sm">
            <span className="material-symbols-outlined text-[18px]" data-icon="download">
              download
            </span>
            Скачать
          </button>
        </div>
      </header>

      {/* Content Grid */}
      <div className="flex-1 grid grid-cols-1 lg:grid-cols-12 gap-6 overflow-hidden min-h-0">
        {/* Left Column: Document Preview & Metadata */}
        <section className="lg:col-span-5 xl:col-span-4 flex flex-col gap-6 overflow-y-auto pr-2 pb-6">
          {/* Document Preview Card */}
          <div className="bg-white rounded-xl p-6 shadow-sm flex flex-col gap-6 shrink-0 relative overflow-hidden group cursor-pointer border border-[#dce3ee]">
            <div className="aspect-[3/4] w-full bg-[#eef3fa] rounded-lg overflow-hidden relative flex items-center justify-center">
              {documentPreview?.previewUrl ? (
                documentPreview.previewKind === 'pdf' ? (
                  <iframe
                    title={`Превью документа ${documentPreview.name}`}
                    src={`${documentPreview.previewUrl}#page=1&toolbar=0&navpanes=0&scrollbar=0`}
                    className="h-full w-full border-0 bg-white"
                  />
                ) : (
                  <img src={documentPreview.previewUrl} alt={`Превью документа ${documentPreview.name}`} className="h-full w-full object-contain bg-white" />
                )
              ) : (
                <span className="material-symbols-outlined text-[64px] text-[#0053db] opacity-50">
                  {document ? getDocumentIcon(document.name) : 'chat'}
                </span>
              )}
            </div>
          </div>

          {/* Metadata Card */}
          <div className="bg-white rounded-xl p-6 shadow-sm border border-[#dce3ee] shrink-0">
            <h2 className="font-['Manrope'] text-[1.125rem] font-bold mb-6">Метаданные</h2>
            <dl className="space-y-5">
              <div className="flex flex-col gap-1">
                <dt className="font-['Inter'] text-[0.75rem] text-[#64748b] uppercase tracking-wider">Название</dt>
                <dd className="font-['Inter'] text-[0.875rem] font-medium break-all">{document ? document.name : 'Без документа'}</dd>
              </div>
              <div className="flex flex-col gap-1">
                <dt className="font-['Inter'] text-[0.75rem] text-[#64748b] uppercase tracking-wider">Статус</dt>
                <dd>
                  {statusInfo ? (
                    <span className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[0.75rem] font-medium ${statusInfo.badge}`}>
                      <span className="material-symbols-outlined text-[14px]" data-icon={statusInfo.icon}>
                        {statusInfo.icon}
                      </span>
                      {statusInfo.label}
                    </span>
                  ) : (
                    <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[0.75rem] font-medium bg-[#e6e8ea] text-[#434655]">
                      Без документа
                    </span>
                  )}
                </dd>
              </div>
              <div className="flex flex-col gap-1">
                <dt className="font-['Inter'] text-[0.75rem] text-[#64748b] uppercase tracking-wider">Дата загрузки</dt>
                <dd className="font-['Inter'] text-[0.875rem] flex items-center gap-2">
                  <span className="material-symbols-outlined text-[16px] text-[#64748b]" data-icon="calendar_today">
                    calendar_today
                  </span>
                  {document ? document.uploadDate : '-'}
                </dd>
              </div>
              <div className="flex flex-col gap-1">
                <dt className="font-['Inter'] text-[0.75rem] text-[#64748b] uppercase tracking-wider">Размер</dt>
                <dd className="font-['Inter'] text-[0.875rem]">{document ? document.size : '-'}</dd>
              </div>
              <div className="flex flex-col gap-1">
                <dt className="font-['Inter'] text-[0.75rem] text-[#64748b] uppercase tracking-wider">Ответственный</dt>
                <dd className="flex items-center gap-3 mt-1">
                  <div className="w-8 h-8 rounded-full bg-[#dbe1ff] text-[#0053db] flex items-center justify-center font-bold text-[0.75rem]">
                    {document ? document.uploadedBy.substring(0, 2).toUpperCase() : 'AI'}
                  </div>
                  <span className="font-['Inter'] text-[0.875rem] font-medium">{document ? document.uploadedBy : 'AI Assistant'}</span>
                </dd>
              </div>
            </dl>
          </div>
        </section>

        {/* Right Column: Chat/Discussion Area */}
        <section className="lg:col-span-7 xl:col-span-8 flex flex-col bg-white rounded-xl shadow-sm border border-[#dce3ee] overflow-hidden">
          {/* Chat Header */}
          <div className="px-6 py-4 border-b border-[#dce3ee] flex justify-between items-center bg-white shrink-0">
            <div className="flex items-center gap-3">
              <span className="material-symbols-outlined text-[#0053db]" data-icon="forum">
                forum
              </span>
              <h2 className="font-['Manrope'] text-[1.125rem] font-bold">Анализ документа</h2>
            </div>
            <button className="text-[#64748b] hover:text-[#0053db] transition-colors p-1.5 rounded-lg hover:bg-[#f3f6fa]">
              <span className="material-symbols-outlined text-[20px]" data-icon="more_vert">
                more_vert
              </span>
            </button>
          </div>

          {/* Messages Area */}
          <div className="flex-1 overflow-y-auto p-6 flex flex-col gap-6 bg-[#f8fbff]">
            {messages.length === 0 ? (
              <div className="flex items-center justify-center h-full text-[#64748b]">
                <div className="text-center">
                  <span className="material-symbols-outlined text-[48px] opacity-30 block mb-2">chat_bubble_outline</span>
                  <p className="font-['Inter'] text-sm">Нет сообщений. Начните диалог о документе</p>
                </div>
              </div>
            ) : (
              <>
                {messages.map((message) =>
                  message.role === 'assistant' ? (
                    <div key={message.id} className="flex items-start gap-4">
                      <div className="mt-1 flex h-8 w-8 items-center justify-center rounded-full bg-[#0053db] text-white flex-shrink-0">
                        <span className="material-symbols-outlined text-base">auto_awesome</span>
                      </div>
                      <article className="max-w-[92%] rounded-2xl rounded-tl-none border-l-4 border-[#0053db] bg-white p-5 shadow-sm">
                        <p className="whitespace-pre-wrap font-['Inter'] text-sm leading-relaxed text-[#1a1d22]">{parseMarkdownFormatting(message.content)}</p>
                        {message.ragSources && message.ragSources.length > 0 && (
                          <div className="mt-4 rounded-xl border border-[#dbe1ff] bg-[#f8fbff] p-3">
                            <div className="mb-2 flex items-center gap-2 font-['Manrope'] text-xs font-bold uppercase tracking-wide text-[#0053db]">
                              <span className="material-symbols-outlined text-[16px]">travel_explore</span>
                              Документы
                            </div>
                            <div className="space-y-2">
                              {message.ragSources.slice(0, 3).map((source, index) => (
                                <div key={`${source.title}-${source.section}-${index}`} className="rounded-lg bg-white px-3 py-2 text-xs text-[#475569] shadow-sm">
                                  <div className="font-semibold text-[#1a1d22]">{source.title || source.sourceFile || 'Документ базы знаний'}</div>
                                  <div className="mt-1 flex flex-wrap gap-x-3 gap-y-1">
                                    {source.section && <span>Раздел: {source.section}</span>}
                                    {source.sourceFile && <span className="truncate">Файл: {source.sourceFile}</span>}
                                  </div>
                                </div>
                              ))}
                            </div>
                          </div>
                        )}
                      </article>
                    </div>
                  ) : (
                    <div key={message.id} className="flex items-start justify-end gap-3">
                      <div className="max-w-[70%] rounded-2xl rounded-tr-none bg-[#0053db] px-4 py-3 text-white shadow-sm">
                        <p className="whitespace-pre-wrap font-['Inter'] text-sm">{parseMarkdownFormatting(message.content)}</p>
                      </div>
                      <div className="h-8 w-8 rounded-full bg-[#dbe1ff] flex-shrink-0" />
                    </div>
                  ),
                )}

                {isLoading && (
                  <div className="flex items-start gap-4">
                    <div className="mt-1 flex h-8 w-8 items-center justify-center rounded-full bg-[#0053db] text-white">
                      <span className="material-symbols-outlined text-base">auto_awesome</span>
                    </div>
                    <article className="max-w-[92%] rounded-2xl rounded-tl-none border-l-4 border-[#0053db] bg-white p-5 shadow-sm">
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
              </>
            )}
          </div>

          {/* Input Area */}
          <div className="p-4 bg-white border-t border-[#dce3ee] shrink-0">
            <form onSubmit={onSubmit} className="flex items-end gap-2 rounded-xl border border-[#dce3ee] bg-white p-2 shadow-sm focus-within:border-[#0053db] focus-within:ring-2 focus-within:ring-[#0053db]/20 transition-all">
              <input
                ref={fileInputRef}
                type="file"
                accept=".pdf,.docx"
                className="hidden"
                onChange={(e) => {
                  const file = e.target.files?.[0] ?? null
                  onFileSelect(file)
                }}
              />
              <button
                type="button"
                className="rounded-lg p-2 text-[#64748b] hover:text-[#0053db] hover:bg-[#f3f6fa] transition-colors shrink-0"
                title={attachmentLimitReached ? 'Достигнут лимит: 1 файл в чате' : 'Прикрепить PDF или DOCX'}
                onClick={() => fileInputRef.current?.click()}
                disabled={isLoading || attachmentLimitReached}
              >
                <span className="material-symbols-outlined">attach_file</span>
              </button>
              {selectedFile && (
                <div className="flex max-w-[240px] items-center gap-2 rounded-xl border border-[#dce3ee] bg-[#f8fbff] px-3 py-2 text-xs text-[#1a1d22]">
                  <span className="truncate font-medium">{selectedFile.name}</span>
                  <button
                    type="button"
                    onClick={() => onFileSelect(null)}
                    className="rounded-full p-1 text-[#64748b] hover:bg-white hover:text-[#93000a]"
                    aria-label="Убрать прикрепленный файл"
                    disabled={isLoading}
                  >
                    <span className="material-symbols-outlined text-[16px]">close</span>
                  </button>
                </div>
              )}
              <textarea
                value={inputValue}
                onChange={(e) => onInputChange(e.target.value)}
                placeholder="Спросите что-нибудь о документе..."
                className="flex-1 bg-transparent border-none focus:ring-0 resize-none text-[0.875rem] py-2.5 px-3 max-h-32 text-[#1a1d22] placeholder:text-[#64748b]/50 focus:outline-none"
                rows={1}
                disabled={isLoading}
              />
              <div className="flex items-center gap-2 self-end shrink-0 p-1">
                <button
                  type="button"
                  className="p-2 text-[#64748b] hover:text-[#0053db] hover:bg-[#f3f6fa] rounded-lg transition-colors"
                  disabled={isLoading}
                >
                  <span className="material-symbols-outlined text-[20px]" data-icon="mic">
                    mic
                  </span>
                </button>
                <button
                  type="submit"
                  className="bg-[#0053db] text-white p-2 rounded-lg hover:opacity-90 transition-opacity shadow-sm disabled:cursor-not-allowed disabled:opacity-50"
                  disabled={isLoading || (!inputValue.trim() && !selectedFile)}
                >
                  <span className="material-symbols-outlined text-[18px]" data-icon="send">
                    send
                  </span>
                </button>
              </div>
            </form>
            {attachmentLimitReached && (
              <div className="mt-2 rounded-lg border border-[#ffd9b8] bg-[#fff4ea] px-3 py-2 text-xs text-[#7d2d00]">
                В этом чате уже есть документ. Чтобы прикрепить новый, создайте новый чат.
              </div>
            )}
            <div className="mt-2 text-center">
              <span className="font-['Inter'] text-[0.6875rem] text-[#64748b]/70">AI Assistant может допускать ошибки. Проверяйте важную информацию.</span>
            </div>
          </div>
        </section>
      </div>
    </main>
  )
}

export default function App() {
  const [messages, setMessages] = useState<ChatMessage[]>(initialMessages)
  const [inputValue, setInputValue] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [errorText, setErrorText] = useState<string | null>(null)
  const [selectedFile, setSelectedFile] = useState<File | null>(null)
  const [pendingFilePreview, setPendingFilePreview] = useState<PendingFilePreview | null>(null)
  const [committedPreviewsBySession, setCommittedPreviewsBySession] = useState<Record<string, PendingFilePreview>>({})
  const [activeView, setActiveView] = useState<'documents' | 'document-details'>('documents')
  const [chats, setChats] = useState<ChatItem[]>([])
  const [selectedChat, setSelectedChat] = useState<ChatItem | null>(null)
  const [selectedDocument, setSelectedDocument] = useState<DocumentItem | null>(null)
  const [isBootstrappingChats, setIsBootstrappingChats] = useState(false)
  const [activeSessionId, setActiveSessionId] = useState('')

  const activeChat = chats.find((chat) => chat.id === activeSessionId) ?? selectedChat
  const attachmentLimitReached = (activeChat?.documents.length ?? 0) >= 1

  const selectedSessionId = activeSessionId
  const activeCommittedPreview = selectedSessionId ? committedPreviewsBySession[selectedSessionId] ?? null : null
  const activeDocumentPreview = pendingFilePreview ?? activeCommittedPreview

  const endOfMessagesRef = useRef<HTMLDivElement | null>(null)
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
  }, [messages, isLoading])

  useEffect(() => {
    return () => {
      if (pendingFilePreview?.previewUrl?.startsWith('blob:')) {
        URL.revokeObjectURL(pendingFilePreview.previewUrl)
      }

      Object.values(committedPreviewsBySession).forEach((preview) => {
        if (preview.previewUrl?.startsWith('blob:')) {
          URL.revokeObjectURL(preview.previewUrl)
        }
      })
    }
  }, [pendingFilePreview, committedPreviewsBySession])

  function revokePreview(preview: PendingFilePreview | null | undefined) {
    if (preview?.previewUrl?.startsWith('blob:')) {
      URL.revokeObjectURL(preview.previewUrl)
    }
  }

  function clearPendingAttachment() {
    revokePreview(pendingFilePreview)
    setSelectedFile(null)
    setPendingFilePreview(null)
  }

  function commitPendingPreview(sessionId: string) {
    if (!pendingFilePreview) {
      return
    }

    setCommittedPreviewsBySession((prev) => {
      const existing = prev[sessionId]
      if (existing && existing.previewUrl !== pendingFilePreview.previewUrl) {
        revokePreview(existing)
      }

      return {
        ...prev,
        [sessionId]: pendingFilePreview,
      }
    })
  }

  useEffect(() => {
    let isMounted = true

    async function bootstrapChats() {
      setIsBootstrappingChats(true)
      try {
        const response = await fetch('/api/chat/sessions')
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}`)
        }

        const data = (await response.json()) as ChatSessionApiResponse[]
        if (isMounted) {
          setChats(data.map(mapApiChatToUi))
        }
      } catch {
        if (isMounted) {
          setChats(mockChats)
        }
      } finally {
        if (isMounted) {
          setIsBootstrappingChats(false)
        }
      }
    }

    void bootstrapChats()

    return () => {
      isMounted = false
    }
  }, [])

  async function validateDocument(file: File): Promise<{ message: string; fullTextContext: string }> {
    const formData = new FormData()
    formData.append('file', file)
    formData.append('summaryOnly', 'true')

    const typeHint = resolveDocumentTypeHint(file.name)
    if (typeHint) {
      formData.append('documentTypeHint', typeHint)
    }

    const response = await fetchWithTimeout('/api/documents/validate', {
      method: 'POST',
      body: formData,
    }, 240000)

    const payload = (await response.json().catch(() => null)) as ValidationApiResponse | null
    if (!response.ok || payload === null) {
      throw new Error('Не удалось выполнить анализ документа.')
    }

    console.info('Документ проанализирован на клиенте', {
      fileName: file.name,
      extractedTextLength: payload.extractedTextLength,
      hasExtractedText: Boolean(payload.extractedText),
    })

    const messageParts: string[] = [`Файл ${file.name} обработан.`]
    if (payload.documentType) {
      messageParts.push(`Тип документа: ${payload.documentType}.`)
    }
    if (payload.summary) {
      messageParts.push(`Выжимка: ${payload.summary}`)
    }
    if (payload.ocrUsed) {
      messageParts.push('Для извлечения текста был применен OCR.')
    }

    const contextParts: string[] = []
    if (payload.documentType) {
      contextParts.push(`Тип документа: ${payload.documentType}`)
    }
    if (payload.extractedText) {
      contextParts.push(payload.extractedText)
    } else if (payload.summary) {
      contextParts.push(payload.summary)
    }

    const fullTextContext = contextParts.join('\n\n').trim()
    return {
      message: messageParts.join('\n\n'),
      fullTextContext,
    }
  }

  async function attachFileToChat(file: File, contextSummary?: string) {
    if (!selectedSessionId) {
      throw new Error('Сначала создайте или откройте чат.')
    }

    const newDoc: DocumentItem = {
      id: `doc-${Date.now()}`,
      name: file.name,
      uploadDate: new Date().toLocaleString('ru-RU'),
      status: 'processing',
      size: `${(file.size / 1024 / 1024).toFixed(1)} MB`,
      uploadedBy: 'Текущий пользователь',
    }

    const response = await fetch(`/api/chat/sessions/${selectedSessionId}/documents`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json; charset=utf-8',
      },
      body: JSON.stringify({
        name: newDoc.name,
        uploadDate: newDoc.uploadDate,
        status: newDoc.status,
        size: newDoc.size,
        uploadedBy: newDoc.uploadedBy,
        contextSummary,
      }),
    })

    if (!response.ok) {
      const payload = (await response.json().catch(() => null)) as { error?: string } | null
      throw new Error(payload?.error || `HTTP ${response.status}`)
    }

    const session = (await response.json()) as ChatSessionApiResponse
    const mapped = mapApiChatToUi(session)

    console.info('Контекст документа сохранен в сессии чата', {
      sessionId: mapped.id,
      fileName: file.name,
      contextLength: contextSummary?.length ?? 0,
    })

    setChats((prev) => [mapped, ...prev.filter((chat) => chat.id !== mapped.id)])
    setSelectedChat(mapped)
    setActiveSessionId(mapped.id)
    setSelectedDocument(mapped.documents[0] ?? null)

    return mapped
  }

  async function renderDocxFirstPageToPng(file: File): Promise<string> {
    console.debug('DOCX preview: start render', {
      fileName: file.name,
      fileSize: file.size,
      fileType: file.type,
    })

    const container = document.createElement('div')
    container.style.position = 'fixed'
    container.style.left = '-10000px'
    container.style.top = '0'
    container.style.width = '794px'
    container.style.background = '#ffffff'
    document.body.appendChild(container)

    try {
      const content = await file.arrayBuffer()
      await renderAsync(content, container, undefined, {
        inWrapper: false,
        ignoreWidth: false,
        ignoreHeight: false,
        breakPages: true,
      })

      // Даем браузеру применить стили и разметку после renderAsync.
      await new Promise<void>((resolve) => requestAnimationFrame(() => resolve()))

      const firstPageCandidates = [
        '.docx-wrapper > section',
        'section.docx',
        '.docx section',
        '.docx-wrapper',
        '.docx',
        'article',
        'div',
      ]

      const firstPage =
        firstPageCandidates
          .map((selector) => container.querySelector(selector))
          .find((node): node is HTMLElement => node instanceof HTMLElement && node.tagName !== 'STYLE') ?? null

      if (!firstPage) {
        throw new Error('DOCX preview: page node was not found')
      }

      const rect = firstPage.getBoundingClientRect()
      console.debug('DOCX preview: page selected', {
        tagName: firstPage.tagName,
        className: firstPage.className,
        width: Math.round(rect.width),
        height: Math.round(rect.height),
      })

      const pngDataUrl = await toPng(firstPage, {
        backgroundColor: '#ffffff',
        pixelRatio: 2,
        // cacheBust добавляет query-string к URL ресурсов. Для blob: URL это ломает загрузку.
        cacheBust: false,
      })

      console.debug('DOCX preview: png generated', {
        fileName: file.name,
        dataUrlLength: pngDataUrl.length,
      })

      return pngDataUrl
    } finally {
      document.body.removeChild(container)
    }
  }

  async function buildSelectedFilePreview(file: File): Promise<PendingFilePreview> {
    const lowerName = file.name.toLowerCase()
    const isPdf = file.type === 'application/pdf' || lowerName.endsWith('.pdf')
    const isDocx = file.type === 'application/vnd.openxmlformats-officedocument.wordprocessingml.document' || lowerName.endsWith('.docx')

    if (isPdf) {
      return {
        name: file.name,
        sizeLabel: `${(file.size / 1024 / 1024).toFixed(1)} MB`,
        kindLabel: 'PDF',
        previewKind: 'pdf',
        previewUrl: URL.createObjectURL(file),
      }
    }

    if (isDocx) {
      const pngDataUrl = await renderDocxFirstPageToPng(file)

      return {
        name: file.name,
        sizeLabel: `${(file.size / 1024 / 1024).toFixed(1)} MB`,
        kindLabel: 'DOCX',
        previewKind: 'image',
        previewUrl: pngDataUrl,
      }
    }

    return {
      name: file.name,
      sizeLabel: `${(file.size / 1024 / 1024).toFixed(1)} MB`,
      kindLabel: 'Документ',
      previewKind: 'image',
      previewUrl: null,
    }
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const trimmed = inputValue.trim()
    if ((!trimmed && !selectedFile) || isLoading) {
      return
    }

    if (!selectedSessionId) {
      setErrorText('Сначала создайте или откройте чат.')
      return
    }

    setIsLoading(true)
    setErrorText(null)

    try {
      const attachedFileName = selectedFile?.name ?? null
      let attachedFileContent: string | null = null

      if (selectedFile) {
        // Сначала получаем содержательный контекст файла, потом фиксируем его в сессии.
        const validation = await validateDocument(selectedFile)
        attachedFileContent = validation.fullTextContext || null

        await attachFileToChat(selectedFile, attachedFileContent ?? undefined)

        setMessages((prev) => [
          ...prev,
          {
            id: `assistant-validation-${Date.now()}`,
            role: 'assistant',
            content: validation.message,
          },
        ])

        if (selectedSessionId) {
          commitPendingPreview(selectedSessionId)
        }

        setSelectedFile(null)
        setPendingFilePreview(null)

        if (!trimmed) {
          return
        }
      }

      const userMessage: ChatMessage = {
        id: `user-${Date.now()}`,
        role: 'user',
        content: trimmed,
      }

      setMessages((prev) => [...prev, userMessage])
      setInputValue('')

      const response = await fetchWithTimeout('/api/chat', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json; charset=utf-8',
        },
        body: JSON.stringify({
          sessionId: selectedSessionId,
          message: trimmed,
          attachedFileName,
          attachedFileContent,
          conversationHistory,
        }),
      }, 120000)

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
          ragSources: data.ragSources ?? [],
        },
      ])

      if (data.sessionId) {
        setChats((prev) =>
          prev.map((chat) =>
            chat.id === data.sessionId
              ? {
                  ...chat,
                  title: data.chatTitle?.trim() || chat.title,
                  updatedAt: data.updatedAt || 'Только что',
                }
              : chat,
          ),
        )

        setSelectedChat((prev) =>
          prev && prev.id === data.sessionId
            ? {
                ...prev,
                title: data.chatTitle?.trim() || prev.title,
                updatedAt: data.updatedAt || 'Только что',
              }
            : prev,
        )
      }
    } catch (error) {
      const isAbort = error instanceof DOMException && error.name === 'AbortError'
      setErrorText(
        isAbort
          ? 'Запрос превысил лимит ожидания. Попробуйте снова или уменьшите размер документа.'
          : 'Не удалось получить ответ от backend. Проверьте доступность /api/chat.',
      )
      setMessages((prev) => [
        ...prev,
        {
          id: `assistant-error-${Date.now()}`,
          role: 'assistant',
          content: isAbort
            ? 'Сервис не успел обработать запрос за отведенное время. Попробуйте еще раз.'
            : 'Сервис временно недоступен. Попробуйте отправить запрос еще раз.',
        },
      ])
    } finally {
      setIsLoading(false)
    }
  }

  async function handleDeleteChat(chatId: string) {
    try {
      const response = await fetch(`/api/chat/sessions/${chatId}`, {
        method: 'DELETE',
      })

      if (!response.ok && response.status !== 204) {
        throw new Error(`HTTP ${response.status}`)
      }

      setChats((prev) => prev.filter((chat) => chat.id !== chatId))

      setCommittedPreviewsBySession((prev) => {
        const { [chatId]: removed, ...rest } = prev
        revokePreview(removed)
        return rest
      })

      if (selectedSessionId === chatId) {
        setActiveView('documents')
        setSelectedChat(null)
        setSelectedDocument(null)
        setActiveSessionId('')
        setMessages(initialMessages)
        setInputValue('')
        clearPendingAttachment()
      }
    } catch {
      setErrorText('Не удалось удалить чат. Попробуйте снова.')
    }
  }

  async function handleOpenDocument(chat: ChatItem) {
    setSelectedChat(chat)
    setActiveSessionId(chat.id)
    setSelectedDocument(chat.documents[0] ?? null)
    setActiveView('document-details')

    try {
      const response = await fetch(`/api/chat/sessions/${chat.id}`)
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`)
      }

      const session = (await response.json()) as ChatSessionApiResponse
      const mapped = mapApiChatToUi(session)
      setSelectedChat(mapped)
      setActiveSessionId(mapped.id)
      setSelectedDocument(mapped.documents[0] ?? null)
      setChats((prev) => [mapped, ...prev.filter((item) => item.id !== mapped.id)])

      const restoredMessages =
        session.messages && session.messages.length > 0
          ? session.messages.map((message, index) => ({
              id: `${session.sessionId}-${index}`,
              role: message.role,
              content: message.content,
            }))
          : initialMessages

      setMessages(restoredMessages)
    } catch {
      setMessages(initialMessages)
    }
  }

  async function handleCreateChat() {
    setErrorText(null)

    try {
      const response = await fetch('/api/chat/sessions', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json; charset=utf-8',
        },
        body: JSON.stringify({}),
      })

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`)
      }

      const session = (await response.json()) as ChatSessionApiResponse
      const mapped = mapApiChatToUi(session)

      setChats((prev) => [mapped, ...prev])
      setSelectedChat(mapped)
      setActiveSessionId(mapped.id)
      setSelectedDocument(null)
      setMessages(initialMessages)
      setInputValue('')
      clearPendingAttachment()
      setActiveView('document-details')
    } catch {
      setErrorText('Не удалось создать новый чат. Попробуйте снова.')
    }
  }

  async function handleFileSelect(file: File | null) {
    if (!file) {
      clearPendingAttachment()
      return
    }

    if (attachmentLimitReached) {
      clearPendingAttachment()
      setErrorText('В этом чате уже есть документ. Добавление нового файла недоступно.')
      return
    }

    setErrorText(null)
    clearPendingAttachment()
    setSelectedFile(file)

    console.debug('File selected for preview', {
      fileName: file.name,
      fileSize: file.size,
      fileType: file.type,
    })

    try {
      const preview = await buildSelectedFilePreview(file)
      setPendingFilePreview(preview)
      console.debug('Preview prepared', {
        fileName: file.name,
        previewKind: preview.previewKind,
        hasPreviewUrl: Boolean(preview.previewUrl),
      })
    } catch (error) {
      console.error('Preview generation failed', {
        fileName: file.name,
        error,
      })
      setPendingFilePreview({
        name: file.name,
        sizeLabel: `${(file.size / 1024 / 1024).toFixed(1)} MB`,
        kindLabel: 'Документ',
        previewKind: 'image',
        previewUrl: null,
      })
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
                onClick={() => setActiveView('documents')}
                type="button"
                className={`flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-left font-['Inter'] text-sm transition-colors ${
                  item.id === 'documents' && activeView === 'documents'
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
            <button
              type="button"
              onClick={handleCreateChat}
              className="w-full rounded-xl bg-gradient-to-br from-[#1d4ed8] to-[#2563eb] px-4 py-3 font-['Manrope'] text-sm font-bold text-white shadow-lg shadow-[#1d4ed8]/25"
            >
              + Новый чат
            </button>
          </div>
        </aside>

        <main className="flex min-w-0 flex-1 flex-col">
          <header className="flex items-center justify-between border-b border-[#dce3ee] bg-[#f8fbff] px-6 py-4 xl:px-8">
            <div>
              <h2 className="font-['Manrope'] text-xl font-bold tracking-tight text-[#111827]">
                {activeView === 'documents' ? 'Все документы' : 'Детали документа'}
              </h2>
              <p className="font-['Inter'] text-xs text-[#64748b]">
                {activeView === 'documents' ? 'Управление чатами и документами' : 'Анализ и обсуждение документа'}
              </p>
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
            {activeView === 'document-details' && selectedChat ? (
              <DocumentDetailsView
                document={selectedDocument}
                chatTitle={selectedChat.title}
                onBack={() => setActiveView('documents')}
                messages={messages}
                inputValue={inputValue}
                isLoading={isLoading}
                errorText={errorText}
                selectedFile={selectedFile}
                documentPreview={activeDocumentPreview}
                attachmentLimitReached={attachmentLimitReached}
                onInputChange={setInputValue}
                onFileSelect={handleFileSelect}
                onSubmit={handleSubmit}
              />
            ) : (
              <>
                {isBootstrappingChats ? (
                  <div className="flex flex-1 items-center justify-center text-[#64748b]">
                    Загрузка чатов...
                  </div>
                ) : (
                  <DocumentsView documents={chats} onOpenDocument={handleOpenDocument} onDeleteChat={handleDeleteChat} />
                )}
              </>
            )}
          </div>
        </main>
      </div>
    </div>
  )
}
