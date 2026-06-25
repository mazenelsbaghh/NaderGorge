package com.nadergorge.parent.ui.screens

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.nadergorge.parent.data.repository.ParentRepository
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

import com.nadergorge.parent.data.api.StudentDetailsResponse
import com.nadergorge.parent.data.storage.LinkedStudent

sealed class LinkingUiState {
    object Idle : LinkingUiState()
    object Loading : LinkingUiState()
    data class Review(val student: LinkedStudent, val details: StudentDetailsResponse) : LinkingUiState()
    data class Success(val studentName: String) : LinkingUiState()
    data class Error(val message: String) : LinkingUiState()
}

class LinkingViewModel(
    private val repository: ParentRepository
) : ViewModel() {

    private val _uiState = MutableStateFlow<LinkingUiState>(LinkingUiState.Idle)
    val uiState: StateFlow<LinkingUiState> = _uiState.asStateFlow()

    private val _code = MutableStateFlow("")
    val code: StateFlow<String> = _code.asStateFlow()

    fun onCodeChange(newCode: String) {
        val filtered = newCode.filter { it.isDigit() }
        if (filtered.length <= 6) {
            _code.value = filtered
        }
    }

    fun linkStudent(deviceToken: String) {
        val currentCode = _code.value.trim()
        if (currentCode.length != 6) {
            _uiState.value = LinkingUiState.Error("الرمز غير صالح، يجب أن يتكون من 6 خانات")
            return
        }

        _uiState.value = LinkingUiState.Loading
        viewModelScope.launch {
            repository.verifyCodeOnly(currentCode, deviceToken)
                .onSuccess { (linkedStudent, details) ->
                    _uiState.value = LinkingUiState.Review(linkedStudent, details)
                }
                .onFailure { error ->
                    _uiState.value = LinkingUiState.Error(
                        error.message ?: "الرمز غير صالح، يرجى التحقق وإعادة المحاولة"
                    )
                }
        }
    }

    fun confirmLink(student: LinkedStudent) {
        repository.saveLinkedStudent(student)
        _uiState.value = LinkingUiState.Success(student.name)
    }

    fun cancelLink() {
        _uiState.value = LinkingUiState.Idle
        _code.value = ""
    }
    
    fun resetState() {
        _uiState.value = LinkingUiState.Idle
        _code.value = ""
    }
}
