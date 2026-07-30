package com.nadergorge.parent.ui.screens

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.nadergorge.parent.data.api.ParentNotificationResponse
import com.nadergorge.parent.data.api.StudentDetailsResponse
import com.nadergorge.parent.data.repository.ParentRepository
import com.nadergorge.parent.data.storage.LinkedStudent
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

sealed class DashboardUiState {
    object Empty : DashboardUiState()
    object Loading : DashboardUiState()
    data class Success(val details: StudentDetailsResponse) : DashboardUiState()
    data class Error(val message: String) : DashboardUiState()
}

class DashboardViewModel(
    private val repository: ParentRepository
) : ViewModel() {

    private val _uiState = MutableStateFlow<DashboardUiState>(DashboardUiState.Empty)
    val uiState: StateFlow<DashboardUiState> = _uiState.asStateFlow()

    private val _linkedStudents = MutableStateFlow<List<LinkedStudent>>(emptyList())
    val linkedStudents: StateFlow<List<LinkedStudent>> = _linkedStudents.asStateFlow()

    private val _activeStudent = MutableStateFlow<LinkedStudent?>(null)
    val activeStudent: StateFlow<LinkedStudent?> = _activeStudent.asStateFlow()

    private val _notifications = MutableStateFlow<List<ParentNotificationResponse>>(emptyList())
    val notifications: StateFlow<List<ParentNotificationResponse>> = _notifications.asStateFlow()

    init {
        loadLinkedStudents()
    }

    fun loadLinkedStudents() {
        val students = repository.getLinkedStudents()
        _linkedStudents.value = students
        val active = repository.getActiveStudent()
        _activeStudent.value = active

        if (active != null) {
            fetchStudentDetails(active.studentId)
        } else {
            _uiState.value = DashboardUiState.Empty
        }
    }

    fun fetchStudentDetails(studentId: String, showLoading: Boolean = true) {
        if (showLoading) {
            _uiState.value = DashboardUiState.Loading
        }
        viewModelScope.launch {
            repository.getStudentDetails(studentId)
                .onSuccess { details ->
                    _uiState.value = DashboardUiState.Success(details)
                    fetchNotifications(studentId)
                }
                .onFailure { error ->
                    _uiState.value = DashboardUiState.Error(
                        error.message ?: "حدث خطأ أثناء تحميل بيانات الطالب"
                    )
                }
        }
    }

    fun fetchNotifications(studentId: String? = _activeStudent.value?.studentId) {
        val currentStudentId = studentId ?: return
        viewModelScope.launch {
            repository.getNotifications(currentStudentId)
                .onSuccess { items -> _notifications.value = items }
        }
    }

    fun markNotificationAsRead(notificationId: String) {
        val studentId = _activeStudent.value?.studentId ?: return
        viewModelScope.launch {
            repository.markNotificationAsRead(studentId, notificationId)
            fetchNotifications(studentId)
        }
    }

    fun refreshActiveStudent() {
        val studentId = _activeStudent.value?.studentId ?: return
        fetchStudentDetails(studentId, showLoading = false)
    }

    fun registerDeviceToken(deviceToken: String) {
        val studentId = _activeStudent.value?.studentId ?: return
        viewModelScope.launch {
            repository.registerDeviceTokenForStudent(studentId, deviceToken)
        }
    }

    fun switchActiveStudent(studentId: String) {
        repository.setActiveStudentId(studentId)
        val active = repository.getActiveStudent()
        _activeStudent.value = active
        _notifications.value = emptyList()
        if (active != null) {
            fetchStudentDetails(studentId)
        }
    }

    fun removeStudent(studentId: String) {
        repository.removeLinkedStudent(studentId)
        loadLinkedStudents()
    }
}
