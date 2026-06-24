package com.nadergorge.parent.ui.screens

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.nadergorge.parent.data.repository.ParentRepository
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

sealed class LinkingUiState {
    object Idle : LinkingUiState()
    object Loading : LinkingUiState()
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
        if (newCode.length <= 6) {
            _code.value = newCode.uppercase()
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
            repository.verifyAndLink(currentCode, deviceToken)
                .onSuccess { linkedStudent ->
                    _uiState.value = LinkingUiState.Success(linkedStudent.name)
                }
                .onFailure { error ->
                    _uiState.value = LinkingUiState.Error(
                        "الرمز غير صالح، يرجى التحقق وإعادة المحاولة"
                    )
                }
        }
    }
    
    fun resetState() {
        _uiState.value = LinkingUiState.Idle
        _code.value = ""
    }
}
