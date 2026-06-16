# API Contract: Student Audio Upload

## Student Audio Upload Endpoint

### HTTP Request
- **Method**: `POST`
- **Path**: `/api/Student/upload-audio`
- **Headers**:
  - `Authorization`: `Bearer <Token>`
  - `Content-Type`: `multipart/form-data`
- **Request Body** (Form Data):
  - `file`: Audio file (IFormFile, max 10MB)

### HTTP Response (200 OK)
- **Content-Type**: `application/json`
- **Body**:
  ```json
  {
    "success": true,
    "message": "Success",
    "errors": null,
    "data": "/uploads/audio/a3b68411-7290-4344-8708-aae3b6841172.mp3"
  }
  ```

### HTTP Response (400 Bad Request - Validation Failures)
- **Mime-type Check Failure**:
  ```json
  {
    "success": false,
    "message": "Only audio files are allowed",
    "errors": ["Only audio files are allowed"]
  }
  ```
- **Extension Check Failure**:
  ```json
  {
    "success": false,
    "message": "Invalid audio file extension",
    "errors": ["Invalid audio file extension"]
  }
  ```
