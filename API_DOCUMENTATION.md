# API Documentation for Library Management System

## Overview
This document describes the RESTful API endpoints implemented for the library management system. The API provides functionality for image-based book search, real-time chat, email notifications, and borrow/return management.

## Base URL
`/api`

## Authentication
Most endpoints require authentication via cookie-based authentication. Some endpoints may have specific role requirements (e.g., librarian-only endpoints).

## API Endpoints

### 1. Image-Based Book Search (ONNX)

#### POST `/api/search-by-image`
Search for books using image similarity.

**Request:**
- Content-Type: `multipart/form-data`
- Parameters:
  - `imageFile` (required): Image file to search with

**Response:**
```json
{
  "success": true,
  "count": 5,
  "results": [
    {
      "BookId": 1,
      "BookTitle": "Book Title",
      "Author": "Author Name",
      "Genre": "Fiction",
      "CoverImage": "/BookImages/image.jpg",
      "Similarity": 0.85
    }
  ]
}
```

### 2. Real-Time Chat

#### GET `/api/chat/messages`
Get chat messages between users.

**Request:**
- Query Parameters:
  - `reader` (optional): Username to get messages for (defaults to current user)

**Response:**
```json
{
  "success": true,
  "messages": [
    {
      "fromUser": "user1",
      "toUser": "thu-thu",
      "message": "Hello",
      "sentAt": "2024-01-01T10:00:00"
    }
  ]
}
```

#### POST `/api/chat/send`
Send a chat message.

**Request:**
- Content-Type: `application/x-www-form-urlencoded`
- Parameters:
  - `toUser` (required): Recipient username
  - `message` (required): Message content

**Response:**
```json
{
  "success": true,
  "message": "Tin nhắn đã được gửi"
}
```

### 3. Email Notifications

#### POST `/api/email/send`
Send an email notification.

**Request:**
- Content-Type: `application/x-www-form-urlencoded`
- Parameters:
  - `toEmail` (required): Recipient email address
  - `subject` (required): Email subject
  - `body` (required): Email body content

**Response:**
```json
{
  "success": true,
  "message": "Email đã được gửi"
}
```

### 4. Borrow/Return Management

#### GET `/api/loans`
Get list of book loans.

**Request:**
- Query Parameters:
  - `status` (optional): Filter by status (Pending, Approved, Returned)

**Response:**
```json
{
  "success": true,
  "loans": [
    {
      "loanId": 1,
      "bookTitle": "Book Title",
      "username": "user1",
      "userEmail": "user@example.com",
      "borrowDate": "2024-01-01T10:00:00",
      "dueDate": "2024-01-15T10:00:00",
      "returnDate": null,
      "status": "Approved"
    }
  ]
}
```

#### POST `/api/loans/{loanId}/approve`
Approve a pending loan request.

**Request:**
- URL Parameters:
  - `loanId` (required): ID of the loan to approve

**Response:**
```json
{
  "success": true,
  "message": "Đã duyệt phiếu mượn thành công"
}
```

#### POST `/api/loans/{loanId}/return`
Mark a book as returned.

**Request:**
- URL Parameters:
  - `loanId` (required): ID of the loan to mark as returned

**Response:**
```json
{
  "success": true,
  "message": "Đã xác nhận trả sách thành công"
}
```

## Error Responses

All endpoints return standard error responses:

```json
{
  "error": "Error message description"
}
```

**HTTP Status Codes:**
- 200: Success
- 400: Bad Request (missing or invalid parameters)
- 401: Unauthorized (authentication required)
- 403: Forbidden (insufficient permissions)
- 404: Not Found (resource not found)
- 500: Internal Server Error

## Implementation Details

### Image Search Technology
- Uses ONNX runtime with MobileNetV2 model for image feature extraction
- Compares image vectors using cosine similarity
- Requires book images to have pre-computed vectors stored in database

### Real-Time Chat
- Uses SignalR for real-time communication (`/chathub`)
- Messages are persisted in database for history
- Supports both reader-librarian and general chat

### Email System
- Uses Gmail SMTP for sending emails
- Configured via `EmailSettings` in appsettings.json
- Background service for overdue notifications runs every minute

### Loan Management
- Integrated with existing librarian controller logic
- Sends email notifications for loan approvals and returns
- Updates book availability automatically

## Testing the APIs

You can test the APIs using tools like:
- Postman
- curl
- Swagger UI (if configured)
- Frontend application integration

Example curl command for image search:
```bash
curl -X POST -F "imageFile=@book_cover.jpg" http://localhost:5000/api/search-by-image
```

## Security Considerations
1. Authentication required for most endpoints
2. Role-based authorization for sensitive operations
3. Input validation on all endpoints
4. File upload restrictions for image search
5. SQL injection protection via Entity Framework