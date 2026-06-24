package com.nadergorge.parent.data.storage

import android.content.Context
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKeys
import com.google.gson.Gson
import com.google.gson.reflect.TypeToken

class StorageService(context: Context) {
    private val masterKeyAlias = MasterKeys.getOrCreate(MasterKeys.AES256_GCM_SPEC)
    
    private val sharedPreferences = EncryptedSharedPreferences.create(
        "parent_secure_prefs",
        masterKeyAlias,
        context,
        EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
        EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM
    )
    
    private val gson = Gson()
    private val studentsKey = "linked_students"
    private val activeStudentIdKey = "active_student_id"

    fun getLinkedStudents(): List<LinkedStudent> {
        val json = sharedPreferences.getString(studentsKey, null) ?: return emptyList()
        return try {
            val type = object : TypeToken<List<LinkedStudent>>() {}.type
            gson.fromJson(json, type) ?: emptyList()
        } catch (e: Exception) {
            emptyList()
        }
    }

    fun saveLinkedStudents(students: List<LinkedStudent>) {
        val json = gson.toJson(students)
        sharedPreferences.edit().putString(studentsKey, json).apply()
    }

    fun addLinkedStudent(student: LinkedStudent) {
        val current = getLinkedStudents().toMutableList()
        // Prevent duplicate IDs
        current.removeAll { it.studentId == student.studentId }
        current.add(student)
        saveLinkedStudents(current)
        if (getActiveStudentId() == null) {
            setActiveStudentId(student.studentId)
        }
    }

    fun removeLinkedStudent(studentId: String) {
        val current = getLinkedStudents().toMutableList()
        current.removeAll { it.studentId == studentId }
        saveLinkedStudents(current)
        if (getActiveStudentId() == studentId) {
            setActiveStudentId(current.firstOrNull()?.studentId)
        }
    }

    fun getActiveStudentId(): String? {
        return sharedPreferences.getString(activeStudentIdKey, null)
    }

    fun setActiveStudentId(studentId: String?) {
        if (studentId == null) {
            sharedPreferences.edit().remove(activeStudentIdKey).apply()
        } else {
            sharedPreferences.edit().putString(activeStudentIdKey, studentId).apply()
        }
    }

    fun getActiveStudent(): LinkedStudent? {
        val activeId = getActiveStudentId() ?: return null
        return getLinkedStudents().firstOrNull { it.studentId == activeId }
    }
}
