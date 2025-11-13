import { API_URL } from '../config'
import type {
  UploadResponse,
  StatusResponse,
  DocumentListResponse,
  QueryRequest,
  QueryResponse,
  ErrorResponse,
} from '../types/api'

/**
 * Upload a PDF document
 */
export async function uploadDocument(file: File): Promise<UploadResponse> {
  const formData = new FormData()
  formData.append('file', file)

  const response = await fetch(`${API_URL}/api/upload`, {
    method: 'POST',
    body: formData,
  })

  if (!response.ok) {
    const error: ErrorResponse = await response.json()
    throw new Error(error.error || `Upload failed: ${response.statusText}`)
  }

  return response.json()
}

/**
 * Get the status of a document by ID
 */
export async function getDocumentStatus(documentId: string): Promise<StatusResponse> {
  const response = await fetch(`${API_URL}/api/status/${documentId}`, {
    method: 'GET',
  })

  if (!response.ok) {
    const error: ErrorResponse = await response.json()
    throw new Error(error.error || `Status check failed: ${response.statusText}`)
  }

  return response.json()
}

/**
 * List all documents, optionally filtered by status
 */
export async function listDocuments(statusFilter?: string, limit?: number): Promise<DocumentListResponse> {
  const params = new URLSearchParams()
  if (statusFilter) params.append('status', statusFilter)
  if (limit) params.append('limit', limit.toString())

  const queryString = params.toString()
  const url = `${API_URL}/api/documents${queryString ? `?${queryString}` : ''}`

  const response = await fetch(url, {
    method: 'GET',
  })

  if (!response.ok) {
    const error: ErrorResponse = await response.json()
    throw new Error(error.error || `List documents failed: ${response.statusText}`)
  }

  return response.json()
}

/**
 * Query documents with a question
 */
export async function queryDocuments(
  question: string,
  documentIds: string[],
  maxChunks?: number
): Promise<QueryResponse> {
  const requestBody: QueryRequest = {
    question,
    documentIds,
    ...(maxChunks && { maxChunks }),
  }

  const response = await fetch(`${API_URL}/api/query`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(requestBody),
  })

  if (!response.ok) {
    const error: ErrorResponse = await response.json()
    throw new Error(error.error || `Query failed: ${response.statusText}`)
  }

  return response.json()
}
