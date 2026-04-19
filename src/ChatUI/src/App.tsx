import { useState, useRef, useEffect, useMemo, FormEvent } from 'react'

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
}

type ChatApiResponse = {
  sessionId: string
  chatTitle?: string
  updatedAt?: string
  response: string
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
  summary?: string
  recommendations?: string[]
  remarks: string[]
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

function DocumentsTable({ chats, onOpenChat }: { chats: ChatItem[]; onOpenChat: (chat: ChatItem) => void }) {
  return (
    <div className="rounded-2xl bg-white shadow-sm overflow-hidden border border-[#dce3ee]">
      <table className="w-full text-left border-collapse">
        <thead>
          <tr className="border-b border-[#dce3ee] text-xs font-medium text-[#64748b] uppercase tracking-wider">
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
              <tr key={chat.id} className="hover:bg-[#f3f6fa] transition-colors">
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
                  <button
                    onClick={() => onOpenChat(chat)}
                    className="text-[#0053db] hover:bg-[#eef3fa] px-3 py-1.5 rounded-lg transition-colors font-medium text-sm"
                  >
                    Открыть
                  </button>
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}

function DocumentsView({ documents, onOpenDocument }: { documents: ChatItem[]; onOpenDocument: (chat: ChatItem) => void }) {
  const [searchQuery, setSearchQuery] = useState('')

  const filteredChats = documents.filter(
    (chat) =>
      chat.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
      chat.documents.some((doc) => doc.name.toLowerCase().includes(searchQuery.toLowerCase())),
  )

  return (
    <main className="flex min-w-0 flex-1 flex-col p-6">
      <header className="mb-8">
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

      <DocumentsTable chats={filteredChats} onOpenChat={onOpenDocument} />

      <div className="flex items-center justify-between mt-6 px-2">
        <span className="text-sm text-[#64748b] font-medium">
          Показано {filteredChats.length} из {documents.length} чатов
        </span>
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
  isValidating,
  errorText,
  selectedFile,
  documentType: _documentType,
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
  isValidating: boolean
  errorText: string | null
  selectedFile: File | null
  documentType: string | null
  attachmentLimitReached: boolean
  onInputChange: (value: string) => void
  onFileSelect: (file: File | null) => void
  onSubmit: (e: FormEvent<HTMLFormElement>) => void
}) {
  const fileInputRef = useRef<HTMLInputElement | null>(null)
  const endOfMessagesRef = useRef<HTMLDivElement | null>(null)

  useEffect(() => {
    endOfMessagesRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages, isLoading, isValidating])

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
              <span className="material-symbols-outlined text-[64px] text-[#0053db] opacity-50">
                {document ? getDocumentIcon(document.name) : 'chat'}
              </span>
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
                        <p className="whitespace-pre-wrap font-['Inter'] text-sm leading-relaxed text-[#1a1d22]">{message.content}</p>
                      </article>
                    </div>
                  ) : (
                    <div key={message.id} className="flex items-start justify-end gap-3">
                      <div className="max-w-[70%] rounded-2xl rounded-tr-none bg-[#0053db] px-4 py-3 text-white shadow-sm">
                        <p className="whitespace-pre-wrap font-['Inter'] text-sm">{message.content}</p>
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
                title={attachmentLimitReached ? 'Достигнут лимит: 3 файла в чате' : 'Прикрепить PDF или DOCX'}
                onClick={() => fileInputRef.current?.click()}
                disabled={isLoading || isValidating || attachmentLimitReached}
              >
                <span className="material-symbols-outlined">attach_file</span>
              </button>
              <textarea
                value={inputValue}
                onChange={(e) => onInputChange(e.target.value)}
                placeholder="Спросите что-нибудь о документе..."
                className="flex-1 bg-transparent border-none focus:ring-0 resize-none text-[0.875rem] py-2.5 px-3 max-h-32 text-[#1a1d22] placeholder:text-[#64748b]/50 focus:outline-none"
                rows={1}
                disabled={isLoading || isValidating}
              />
              <div className="flex items-center gap-2 self-end shrink-0 p-1">
                <button
                  type="button"
                  className="p-2 text-[#64748b] hover:text-[#0053db] hover:bg-[#f3f6fa] rounded-lg transition-colors"
                  disabled={isLoading || isValidating}
                >
                  <span className="material-symbols-outlined text-[20px]" data-icon="mic">
                    mic
                  </span>
                </button>
                <button
                  type="submit"
                  className="bg-[#0053db] text-white p-2 rounded-lg hover:opacity-90 transition-opacity shadow-sm disabled:cursor-not-allowed disabled:opacity-50"
                  disabled={isLoading || isValidating || (!inputValue.trim() && !selectedFile)}
                >
                  <span className="material-symbols-outlined text-[18px]" data-icon="send">
                    send
                  </span>
                </button>
              </div>
            </form>
            {attachmentLimitReached && (
              <div className="mt-2 rounded-lg border border-[#ffd9b8] bg-[#fff4ea] px-3 py-2 text-xs text-[#7d2d00]">
                В этом чате уже 3 документа. Чтобы прикрепить новый, удалите один из текущих или создайте новый чат.
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
  const [isValidating, setIsValidating] = useState(false)
  const [errorText, setErrorText] = useState<string | null>(null)
  const [selectedFile, setSelectedFile] = useState<File | null>(null)
  const [documentType, setDocumentType] = useState<string | null>(null)
  const [activeView, setActiveView] = useState<'documents' | 'document-details'>('documents')
  const [chats, setChats] = useState<ChatItem[]>([])
  const [selectedChat, setSelectedChat] = useState<ChatItem | null>(null)
  const [selectedDocument, setSelectedDocument] = useState<DocumentItem | null>(null)
  const [isBootstrappingChats, setIsBootstrappingChats] = useState(false)
  const attachmentLimitReached = (selectedChat?.documents.length ?? 0) >= 3

  const selectedSessionId = selectedChat?.id ?? ''

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
  }, [messages, isLoading, isValidating])

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

  async function runDocumentValidation(file: File) {
    if (attachmentLimitReached) {
      setErrorText('В этом чате уже 3 документа. Добавление нового файла недоступно.')
      return
    }

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

      const summaryParts: string[] = []
      const statusLabel =
        data.status === 'ok'
          ? 'успешно'
          : data.status === 'template_not_found'
            ? 'с частичной определенностью'
            : 'с замечаниями'

      summaryParts.push(`Проверка файла ${file.name} завершена ${statusLabel}.`)

      if (data.documentType) {
        summaryParts.push(`Тип документа: ${data.documentType}.`)
      }

      if (typeof data.classificationConfidence === 'number') {
        summaryParts.push(`Уверенность классификации: ${(data.classificationConfidence * 100).toFixed(1)}%.`)
      }

      if (data.ocrUsed) {
        summaryParts.push('Для документа был применен OCR.')
      }

      if (data.summary) {
        summaryParts.push(`Выжимка: ${data.summary}`)
      }

      if (data.recommendations && data.recommendations.length > 0) {
        summaryParts.push(`Рекомендации:\n${data.recommendations.map((item, index) => `${index + 1}. ${item}`).join('\n')}`)
      }

      if (data.remarks.length > 0) {
        summaryParts.push(`Замечания (${data.remarks.length}):\n${data.remarks.map((item) => `- ${item}`).join('\n')}`)
      }

      const summaryText = summaryParts.join('\n\n')

      setMessages((prev) => [
        ...prev,
        {
          id: `assistant-validation-${Date.now()}`,
          role: 'assistant',
          content: summaryText,
        },
      ])

      // Добавить новый документ в выбранный чат
      const newDoc: DocumentItem = {
        id: `doc-${Date.now()}`,
        name: file.name,
        uploadDate: new Date().toLocaleString('ru-RU'),
        status: data.status === 'ok' ? 'checked' : data.status === 'bad_request' ? 'error' : 'processing',
        size: `${(file.size / 1024 / 1024).toFixed(1)} MB`,
        uploadedBy: 'Текущий пользователь',
      }

      if (selectedChat) {
        try {
          const attachResponse = await fetch(`/api/chat/sessions/${selectedChat.id}/documents`, {
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
            }),
          })

          if (attachResponse.ok) {
            const session = (await attachResponse.json()) as ChatSessionApiResponse
            const mapped = mapApiChatToUi(session)

            setChats((prev) => [mapped, ...prev.filter((chat) => chat.id !== mapped.id)])
            setSelectedChat(mapped)
            setSelectedDocument(mapped.documents[0] ?? null)
          } else {
            setChats((prev) =>
              prev.map((chat) =>
                chat.id === selectedChat.id
                  ? {
                      ...chat,
                      documents: [newDoc, ...chat.documents].slice(0, 3),
                      updatedAt: 'Только что',
                    }
                  : chat,
              ),
            )
            setSelectedDocument(newDoc)
          }
        } catch {
          setChats((prev) =>
            prev.map((chat) =>
              chat.id === selectedChat.id
                ? {
                    ...chat,
                    documents: [newDoc, ...chat.documents].slice(0, 3),
                    updatedAt: 'Только что',
                  }
                : chat,
            ),
          )
          setSelectedDocument(newDoc)
        }
      }
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
      if (attachmentLimitReached) {
        setErrorText('В этом чате уже 3 документа. Добавление нового файла недоступно.')
        setSelectedFile(null)
        return
      }

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

    if (!selectedSessionId) {
      setErrorText('Сначала создайте или откройте чат.')
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
          sessionId: selectedSessionId,
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

  async function handleOpenDocument(chat: ChatItem) {
    setSelectedChat(chat)
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
      setSelectedDocument(null)
      setMessages(initialMessages)
      setInputValue('')
      setSelectedFile(null)
      setDocumentType(null)
      setActiveView('document-details')
    } catch {
      setErrorText('Не удалось создать новый чат. Попробуйте снова.')
    }
  }

  function handleFileSelect(file: File | null) {
    if (!file) {
      setSelectedFile(null)
      return
    }

    if (attachmentLimitReached) {
      setSelectedFile(null)
      setErrorText('В этом чате уже 3 документа. Добавление нового файла недоступно.')
      return
    }

    setErrorText(null)
    setSelectedFile(file)
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
                isValidating={isValidating}
                errorText={errorText}
                selectedFile={selectedFile}
                documentType={documentType}
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
                  <DocumentsView documents={chats} onOpenDocument={handleOpenDocument} />
                )}
              </>
            )}
          </div>
        </main>
      </div>
    </div>
  )
}
