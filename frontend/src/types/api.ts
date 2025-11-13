// Upload types
export interface UploadResponse {
  documentId: string
  fileName: string
  message: string
  statusEndpoint: string
}

// Status types
export interface StatusResponse {
  documentId: string
  fileName: string
  status: 'uploaded' | 'processing' | 'completed' | 'failed'
  uploadedAt: string
  processedAt?: string
  errorMessage?: string
  errorStep?: string
  pageCount?: number
  chunkCount?: number
}

// Document list types
export interface DocumentSummary {
  documentId: string
  fileName: string
  status: 'uploaded' | 'processing' | 'completed' | 'failed'
  uploadedAt: string
  processedAt?: string
  pageCount?: number
  chunkCount?: number
}

export interface DocumentListResponse {
  documents: DocumentSummary[]
  totalCount: number
}

// Query types
export interface QueryRequest {
  question: string
  documentIds: string[]
  maxChunks?: number
}

export interface Citation {
  documentTitle: string
  pageNumber: number
  excerpt: string
  sectionTitle?: string
}

export interface QueryResponse {
  answer: string
  confidence: number
  citations: Citation[]
  processingTimeMs: number
}

// Error response type
export interface ErrorResponse {
  error: string
}
