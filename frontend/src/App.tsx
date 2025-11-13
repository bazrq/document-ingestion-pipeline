import { useState, useEffect, useRef } from 'react'
import * as api from './services/apiClient'
import * as storage from './services/localStorage'
import type { DocumentSummary, QueryResponse, Citation } from './types/api'

function App() {
  // Upload state
  const [uploadFile, setUploadFile] = useState<File | null>(null)
  const [uploading, setUploading] = useState(false)
  const [uploadResult, setUploadResult] = useState<string>('')
  const [dragActive, setDragActive] = useState(false)
  const fileInputRef = useRef<HTMLInputElement>(null)

  // Documents list state
  const [documents, setDocuments] = useState<DocumentSummary[]>([])
  const [loadingDocs, setLoadingDocs] = useState(false)
  const [selectedDocIds, setSelectedDocIds] = useState<Set<string>>(new Set())

  // Recent documents state
  const [recentDocs, setRecentDocs] = useState(storage.getRecentDocuments())

  // Status check state
  const [statusDocId, setStatusDocId] = useState('')
  const [statusResult, setStatusResult] = useState<any>(null)
  const [checkingStatus, setCheckingStatus] = useState(false)

  // Query state
  const [question, setQuestion] = useState('')
  const [querying, setQuerying] = useState(false)
  const [queryResult, setQueryResult] = useState<QueryResponse | null>(null)

  // Error state
  const [error, setError] = useState('')

  // Load documents on mount
  useEffect(() => {
    loadDocuments()
  }, [])

  // Load all documents
  const loadDocuments = async () => {
    setLoadingDocs(true)
    setError('')
    try {
      const result = await api.listDocuments()
      setDocuments(result.documents)
    } catch (err) {
      setError(`Failed to load documents: ${err}`)
    } finally {
      setLoadingDocs(false)
    }
  }

  // Handle file drag events
  const handleDrag = (e: React.DragEvent) => {
    e.preventDefault()
    e.stopPropagation()
    if (e.type === 'dragenter' || e.type === 'dragover') {
      setDragActive(true)
    } else if (e.type === 'dragleave') {
      setDragActive(false)
    }
  }

  // Handle file drop
  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault()
    e.stopPropagation()
    setDragActive(false)

    if (e.dataTransfer.files && e.dataTransfer.files[0]) {
      const file = e.dataTransfer.files[0]
      if (file.type === 'application/pdf') {
        setUploadFile(file)
      } else {
        setError('Please upload a PDF file')
      }
    }
  }

  // Handle file input change
  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files[0]) {
      setUploadFile(e.target.files[0])
    }
  }

  // Upload document
  const handleUpload = async () => {
    if (!uploadFile) return

    setUploading(true)
    setError('')
    setUploadResult('')
    try {
      const result = await api.uploadDocument(uploadFile)
      setUploadResult(`Success! Document ID: ${result.documentId}`)

      // Save to recent documents
      storage.saveRecentDocument(result.documentId, result.fileName)
      setRecentDocs(storage.getRecentDocuments())

      // Refresh document list
      await loadDocuments()

      // Clear file
      setUploadFile(null)
      if (fileInputRef.current) fileInputRef.current.value = ''
    } catch (err) {
      setError(`Upload failed: ${err}`)
    } finally {
      setUploading(false)
    }
  }

  // Check document status
  const handleCheckStatus = async () => {
    if (!statusDocId.trim()) return

    setCheckingStatus(true)
    setError('')
    setStatusResult(null)
    try {
      const result = await api.getDocumentStatus(statusDocId.trim())
      setStatusResult(result)
    } catch (err) {
      setError(`Status check failed: ${err}`)
    } finally {
      setCheckingStatus(false)
    }
  }

  // Toggle document selection
  const toggleDocSelection = (docId: string) => {
    const newSet = new Set(selectedDocIds)
    if (newSet.has(docId)) {
      newSet.delete(docId)
    } else {
      newSet.add(docId)
    }
    setSelectedDocIds(newSet)
  }

  // Select document from recent
  const selectRecentDoc = (docId: string) => {
    const newSet = new Set(selectedDocIds)
    newSet.add(docId)
    setSelectedDocIds(newSet)
  }

  // Submit query
  const handleQuery = async () => {
    if (!question.trim() || selectedDocIds.size === 0) return

    setQuerying(true)
    setError('')
    setQueryResult(null)
    try {
      const result = await api.queryDocuments(question.trim(), Array.from(selectedDocIds))
      setQueryResult(result)
    } catch (err) {
      setError(`Query failed: ${err}`)
    } finally {
      setQuerying(false)
    }
  }

  return (
    <div className="min-h-screen bg-gray-50 p-8">
      <div className="max-w-6xl mx-auto">
        <h1 className="text-3xl font-bold mb-8 text-gray-800">Document QA System</h1>

        {/* Error display */}
        {error && (
          <div className="mb-6 p-4 bg-red-50 border border-red-200 rounded text-red-700">
            {error}
          </div>
        )}

        {/* Upload Section */}
        <section className="mb-8 p-6 bg-white rounded-lg shadow">
          <h2 className="text-xl font-semibold mb-4 text-gray-700">Upload PDF Document</h2>
          <div
            className={`border-2 border-dashed rounded-lg p-8 text-center ${
              dragActive ? 'border-blue-500 bg-blue-50' : 'border-gray-300'
            }`}
            onDragEnter={handleDrag}
            onDragLeave={handleDrag}
            onDragOver={handleDrag}
            onDrop={handleDrop}
          >
            <input
              ref={fileInputRef}
              type="file"
              accept=".pdf"
              onChange={handleFileChange}
              className="hidden"
              id="file-upload"
            />
            <label htmlFor="file-upload" className="cursor-pointer">
              <p className="text-gray-600 mb-2">Drag and drop a PDF file here, or click to browse</p>
              <p className="text-sm text-gray-400">Max size: 100MB</p>
            </label>
          </div>

          {uploadFile && (
            <div className="mt-4">
              <p className="text-sm text-gray-600">Selected: {uploadFile.name}</p>
              <button
                onClick={handleUpload}
                disabled={uploading}
                className="mt-2 px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:bg-gray-400 disabled:cursor-not-allowed"
              >
                {uploading ? 'Uploading...' : 'Upload'}
              </button>
            </div>
          )}

          {uploadResult && (
            <div className="mt-4 p-3 bg-green-50 border border-green-200 rounded text-green-700">
              {uploadResult}
            </div>
          )}
        </section>

        {/* Document List Section */}
        <section className="mb-8 p-6 bg-white rounded-lg shadow">
          <div className="flex justify-between items-center mb-4">
            <h2 className="text-xl font-semibold text-gray-700">All Documents</h2>
            <button
              onClick={loadDocuments}
              disabled={loadingDocs}
              className="px-3 py-1 text-sm bg-gray-200 rounded hover:bg-gray-300 disabled:bg-gray-100"
            >
              {loadingDocs ? 'Loading...' : 'Refresh'}
            </button>
          </div>

          {documents.length === 0 ? (
            <p className="text-gray-500">No documents uploaded yet.</p>
          ) : (
            <div className="space-y-2 max-h-64 overflow-y-auto">
              {documents.map(doc => (
                <div
                  key={doc.documentId}
                  className="flex items-center gap-3 p-2 border rounded hover:bg-gray-50"
                >
                  <input
                    type="checkbox"
                    checked={selectedDocIds.has(doc.documentId)}
                    onChange={() => toggleDocSelection(doc.documentId)}
                    className="w-4 h-4"
                  />
                  <div className="flex-1 min-w-0">
                    <p className="font-medium text-sm truncate">{doc.fileName}</p>
                    <p className="text-xs text-gray-500">
                      ID: {doc.documentId.substring(0, 8)}... | Status: {doc.status} |
                      Uploaded: {new Date(doc.uploadedAt).toLocaleString()}
                    </p>
                  </div>
                  <span className={`px-2 py-1 text-xs rounded ${
                    doc.status === 'completed' ? 'bg-green-100 text-green-700' :
                    doc.status === 'processing' ? 'bg-yellow-100 text-yellow-700' :
                    doc.status === 'failed' ? 'bg-red-100 text-red-700' :
                    'bg-gray-100 text-gray-700'
                  }`}>
                    {doc.status}
                  </span>
                </div>
              ))}
            </div>
          )}

          {selectedDocIds.size > 0 && (
            <p className="mt-3 text-sm text-gray-600">
              {selectedDocIds.size} document(s) selected for querying
            </p>
          )}
        </section>

        {/* Recent Uploads Section */}
        {recentDocs.length > 0 && (
          <section className="mb-8 p-6 bg-white rounded-lg shadow">
            <h2 className="text-xl font-semibold mb-4 text-gray-700">Recent Uploads</h2>
            <div className="flex flex-wrap gap-2">
              {recentDocs.map(doc => (
                <button
                  key={doc.id}
                  onClick={() => selectRecentDoc(doc.id)}
                  className={`px-3 py-1 text-sm rounded border ${
                    selectedDocIds.has(doc.id)
                      ? 'bg-blue-100 border-blue-300 text-blue-700'
                      : 'bg-gray-50 border-gray-300 hover:bg-gray-100'
                  }`}
                >
                  {doc.fileName}
                </button>
              ))}
            </div>
          </section>
        )}

        {/* Status Check Section */}
        <section className="mb-8 p-6 bg-white rounded-lg shadow">
          <h2 className="text-xl font-semibold mb-4 text-gray-700">Check Document Status</h2>
          <div className="flex gap-2">
            <input
              type="text"
              value={statusDocId}
              onChange={(e) => setStatusDocId(e.target.value)}
              placeholder="Enter document ID"
              className="flex-1 px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
            <button
              onClick={handleCheckStatus}
              disabled={checkingStatus || !statusDocId.trim()}
              className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:bg-gray-400 disabled:cursor-not-allowed"
            >
              {checkingStatus ? 'Checking...' : 'Check Status'}
            </button>
          </div>

          {statusResult && (
            <div className="mt-4 p-4 bg-gray-50 rounded border">
              <p className="font-medium">File: {statusResult.fileName}</p>
              <p className="text-sm">Status: <span className="font-semibold">{statusResult.status}</span></p>
              <p className="text-sm">Uploaded: {new Date(statusResult.uploadedAt).toLocaleString()}</p>
              {statusResult.processedAt && (
                <p className="text-sm">Processed: {new Date(statusResult.processedAt).toLocaleString()}</p>
              )}
              {statusResult.pageCount && <p className="text-sm">Pages: {statusResult.pageCount}</p>}
              {statusResult.chunkCount && <p className="text-sm">Chunks: {statusResult.chunkCount}</p>}
              {statusResult.errorMessage && (
                <p className="text-sm text-red-600">Error: {statusResult.errorMessage}</p>
              )}
            </div>
          )}
        </section>

        {/* Query Section */}
        <section className="mb-8 p-6 bg-white rounded-lg shadow">
          <h2 className="text-xl font-semibold mb-4 text-gray-700">Ask a Question</h2>
          <div className="space-y-3">
            <textarea
              value={question}
              onChange={(e) => setQuestion(e.target.value)}
              placeholder="Enter your question here..."
              rows={3}
              className="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
            <button
              onClick={handleQuery}
              disabled={querying || !question.trim() || selectedDocIds.size === 0}
              className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:bg-gray-400 disabled:cursor-not-allowed"
            >
              {querying ? 'Querying...' : 'Ask Question'}
            </button>
            {selectedDocIds.size === 0 && (
              <p className="text-sm text-yellow-600">Please select at least one document from the list above</p>
            )}
          </div>
        </section>

        {/* Results Section */}
        {queryResult && (
          <section className="mb-8 p-6 bg-white rounded-lg shadow">
            <h2 className="text-xl font-semibold mb-4 text-gray-700">Answer</h2>
            <div className="space-y-4">
              <div className="p-4 bg-blue-50 rounded border border-blue-200">
                <p className="text-gray-800 whitespace-pre-wrap">{queryResult.answer}</p>
              </div>

              <div className="flex gap-4 text-sm text-gray-600">
                <p>Confidence: <span className="font-semibold">{(queryResult.confidence * 100).toFixed(1)}%</span></p>
                <p>Processing time: <span className="font-semibold">{queryResult.processingTimeMs}ms</span></p>
              </div>

              {queryResult.citations.length > 0 && (
                <div>
                  <h3 className="font-semibold mb-2 text-gray-700">Sources ({queryResult.citations.length})</h3>
                  <div className="space-y-2">
                    {queryResult.citations.map((citation: Citation, idx: number) => (
                      <div key={idx} className="p-3 bg-gray-50 rounded border text-sm">
                        <p className="font-medium">{citation.documentTitle} - Page {citation.pageNumber}</p>
                        {citation.sectionTitle && (
                          <p className="text-gray-600 text-xs">Section: {citation.sectionTitle}</p>
                        )}
                        <p className="text-gray-700 mt-1 italic">"{citation.excerpt}"</p>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </section>
        )}
      </div>
    </div>
  )
}

export default App
