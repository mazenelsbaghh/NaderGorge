package com.nadergorge.parent.service

import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.asSharedFlow

object ParentRefreshBus {
    private val _events = MutableSharedFlow<String?>(extraBufferCapacity = 8)
    val events = _events.asSharedFlow()

    fun notifyStudentChanged(studentId: String?) {
        _events.tryEmit(studentId)
    }
}
