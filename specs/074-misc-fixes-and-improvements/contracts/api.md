# API Contract: Miscellaneous Fixes and Improvements

This document lists the new backend endpoint contracts.

## 1. Cancel Access Grant

Allows an admin to cancel a student's package access grant and optionally refund the cost of the package to the student's wallet balance.

* **URL**: `/api/admin/users/students/{userId}/packages/{accessGrantId}/cancel`
* **Method**: `POST`
* **Auth Required**: Yes (`Admin` role)
* **Headers**:
  * `Authorization: Bearer <Token>`
  * `Content-Type: application/json`

* **Request Body**:
  ```json
  {
    "refundBalance": true
  }
  ```

* **Success Response**:
  * **Code**: `200 OK`
  * **Content**:
    ```json
    {
      "success": true,
      "message": "تم إلغاء الاشتراك بنجاح وتعديل الرصيد.",
      "data": true
    }
    ```

* **Error Responses**:
  * **Code**: `400 Bad Request` (Invalid ID, package already canceled, or database error)
  * **Code**: `401 Unauthorized` (Not authenticated)
  * **Code**: `403 Forbidden` (Not an admin)
  * **Code**: `404 Not Found` (Grant not found)
