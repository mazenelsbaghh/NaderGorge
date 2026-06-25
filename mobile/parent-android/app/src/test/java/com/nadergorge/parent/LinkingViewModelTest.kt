package com.nadergorge.parent

import com.nadergorge.parent.data.repository.ParentRepository
import com.nadergorge.parent.data.storage.LinkedStudent
import com.nadergorge.parent.data.api.StudentDetailsResponse
import com.nadergorge.parent.data.api.AttendanceInfo
import com.nadergorge.parent.ui.screens.LinkingUiState
import com.nadergorge.parent.ui.screens.LinkingViewModel
import io.mockk.*
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.*
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class LinkingViewModelTest {

    private val testDispatcher = StandardTestDispatcher()
    private val repository: ParentRepository = mockk(relaxed = true)
    private lateinit var viewModel: LinkingViewModel

    private val mockDetails = StudentDetailsResponse(
        studentName = "أحمد محمد",
        grade = "الصف الأول الثانوي",
        school = "مدرسة الحرية",
        avatarSlug = null,
        attendance = AttendanceInfo(10, 8, 80.0),
        exams = emptyList(),
        homeworks = emptyList(),
        warnings = emptyList()
    )

    @Before
    fun setUp() {
        Dispatchers.setMain(testDispatcher)
        viewModel = LinkingViewModel(repository)
    }

    @After
    fun tearDown() {
        Dispatchers.resetMain()
    }

    @Test
    fun `onCodeChange limits characters to 6 and filters non-digits`() {
        viewModel.onCodeChange("123")
        assertEquals("123", viewModel.code.value)

        viewModel.onCodeChange("1234567")
        assertEquals("123", viewModel.code.value) // shouldn't update if length > 6

        viewModel.onCodeChange("123abc456")
        assertEquals("123456", viewModel.code.value) // filters out "abc"
    }

    @Test
    fun `linkStudent with invalid code length sets Error state`() {
        viewModel.onCodeChange("123")
        viewModel.linkStudent("token")
        
        testDispatcher.scheduler.advanceUntilIdle()
        
        assertTrue(viewModel.uiState.value is LinkingUiState.Error)
        assertEquals("الرمز غير صالح، يجب أن يتكون من 6 خانات", (viewModel.uiState.value as LinkingUiState.Error).message)
        coVerify(exactly = 0) { repository.verifyCodeOnly(any(), any()) }
    }

    @Test
    fun `linkStudent success updates state to Review`() {
        val student = LinkedStudent("id123", "أحمد محمد", "token123")
        coEvery { repository.verifyCodeOnly("123456", "token") } returns Result.success(Pair(student, mockDetails))

        viewModel.onCodeChange("123456")
        viewModel.linkStudent("token")
        
        assertEquals(LinkingUiState.Loading, viewModel.uiState.value)
        
        testDispatcher.scheduler.advanceUntilIdle()
        
        assertTrue(viewModel.uiState.value is LinkingUiState.Review)
        val reviewState = viewModel.uiState.value as LinkingUiState.Review
        assertEquals(student, reviewState.student)
        assertEquals(mockDetails, reviewState.details)
    }

    @Test
    fun `linkStudent failure updates state to Error`() {
        coEvery { repository.verifyCodeOnly("123456", "token") } returns Result.failure(Exception("Invalid code"))

        viewModel.onCodeChange("123456")
        viewModel.linkStudent("token")
        
        assertEquals(LinkingUiState.Loading, viewModel.uiState.value)
        
        testDispatcher.scheduler.advanceUntilIdle()
        
        assertTrue(viewModel.uiState.value is LinkingUiState.Error)
        assertEquals("Invalid code", (viewModel.uiState.value as LinkingUiState.Error).message)
    }

    @Test
    fun `confirmLink saves student and updates state to Success`() {
        val student = LinkedStudent("id123", "أحمد محمد", "token123")
        
        viewModel.confirmLink(student)
        
        verify(exactly = 1) { repository.saveLinkedStudent(student) }
        assertEquals(LinkingUiState.Success("أحمد محمد"), viewModel.uiState.value)
    }

    @Test
    fun `cancelLink resets state to Idle`() {
        viewModel.onCodeChange("123")
        viewModel.cancelLink()
        
        assertEquals("", viewModel.code.value)
        assertEquals(LinkingUiState.Idle, viewModel.uiState.value)
    }
}
