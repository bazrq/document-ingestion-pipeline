const RECENT_DOCUMENTS_KEY = 'documentqa_recent_documents'
const MAX_RECENT_DOCUMENTS = 10

export interface RecentDocument {
  id: string
  fileName: string
  uploadTime: string
}

/**
 * Save a document to recent uploads history
 */
export function saveRecentDocument(id: string, fileName: string): void {
  const recent = getRecentDocuments()

  // Remove if already exists (to update timestamp)
  const filtered = recent.filter(doc => doc.id !== id)

  // Add to beginning
  filtered.unshift({
    id,
    fileName,
    uploadTime: new Date().toISOString(),
  })

  // Keep only MAX_RECENT_DOCUMENTS
  const trimmed = filtered.slice(0, MAX_RECENT_DOCUMENTS)

  localStorage.setItem(RECENT_DOCUMENTS_KEY, JSON.stringify(trimmed))
}

/**
 * Get all recent documents
 */
export function getRecentDocuments(): RecentDocument[] {
  try {
    const stored = localStorage.getItem(RECENT_DOCUMENTS_KEY)
    if (!stored) return []

    return JSON.parse(stored) as RecentDocument[]
  } catch (error) {
    console.error('Error reading recent documents from localStorage:', error)
    return []
  }
}

/**
 * Clear all recent documents
 */
export function clearRecentDocuments(): void {
  localStorage.removeItem(RECENT_DOCUMENTS_KEY)
}

/**
 * Remove a specific document from recent history
 */
export function removeRecentDocument(id: string): void {
  const recent = getRecentDocuments()
  const filtered = recent.filter(doc => doc.id !== id)
  localStorage.setItem(RECENT_DOCUMENTS_KEY, JSON.stringify(filtered))
}
