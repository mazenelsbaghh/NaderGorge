package com.nadergorge.parent

import com.nadergorge.parent.data.api.AttendanceInfo
import com.nadergorge.parent.data.api.StudentDetailsResponse
import com.nadergorge.parent.data.repository.ParentRepository
import com.nadergorge.parent.data.storage.LinkedStudent
import com.nadergorge.parent.ui.screens.DashboardUiState
import com.nadergorge.parent.ui.screens.DashboardViewModel
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
class DashboardViewModelTest {

    private val testDispatcher = StandardTestDispatcher()
    private val repository: ParentRepository = mockk(relaxed = true)
    private lateinit var viewModel: DashboardViewModel

    private val mockDetails = StudentDetailsResponse(
        studentName = "أحمد محمد",
        grade = "الصف الثالث الثانوي",
        school = "مدرسة الأورمان",
        avatarSlug = null,
        attendance = AttendanceInfo(20, 18, 90.0),
        exams = emptyList(),
        homeworks = emptyList(),
        warnings = emptyList()
    )

    @Before
    fun setUp() {
        Dispatchers.setMain(testDispatcher)
    }

    @After
    fun tearDown() {
        Dispatchers.resetMain()
    }

    @Test
    fun `initialization with no active student sets Empty state`() {
        every { repository.getLinkedStudents() } returns emptyList()
        every { repository.getActiveStudent() } returns null

        viewModel = DashboardViewModel(repository)
        testDispatcher.scheduler.advanceUntilIdle()

        assertEquals(DashboardUiState.Empty, viewModel.uiState.value)
        assertEquals(0, viewModel.linkedStudents.value.size)
        assertEquals(null, viewModel.activeStudent.value)
    }

    @Test
    fun `initialization with active student loads details successfully`() {
        val student = LinkedStudent("id123", "أحمد محمد", "token123")
        every { repository.getLinkedStudents() } returns listOf(student)
        every { repository.getActiveStudent() } returns student
        coEvery { repository.getStudentDetails("id123") } returns Result.success(mockDetails)

        viewModel = DashboardViewModel(repository)
        
        assertEquals(DashboardUiState.Loading, viewModel.uiState.value)
        
        testDispatcher.scheduler.advanceUntilIdle()

        assertEquals(DashboardUiState.Success(mockDetails), viewModel.uiState.value)
        assertEquals(student, viewModel.activeStudent.value)
    }

    @Test
    fun `switchActiveStudent updates active student and fetches details`() {
        val student1 = LinkedStudent("id1", "أحمد", "token1")
        val student2 = LinkedStudent("id2", "محمد", "token2")
        every { repository.getLinkedStudents() } returns listOf(student1, student2)
        every { repository.getActiveStudent() } returns student1
        coEvery { repository.getStudentDetails("id1") } returns Result.success(mockDetails)
        
        viewModel = DashboardViewModel(repository)
        testDispatcher.scheduler.advanceUntilIdle()

        // Switch to student2
        every { repository.getActiveStudent() } returns student2
        coEvery { repository.getStudentDetails("id2") } returns Result.success(
            mockDetails.copy(studentName = "محمد")
        )

        viewModel.switchActiveStudent("id2")
        
        assertEquals(DashboardUiState.Loading, viewModel.uiState.value)
        
        testDispatcher.scheduler.advanceUntilIdle()

        verify { repository.setActiveStudentId("id2") }
        assertEquals(student2, viewModel.activeStudent.value)
        assertTrue(viewModel.uiState.value is DashboardUiState.Success)
        assertEquals("محمد", (viewModel.uiState.value as DashboardUiState.Success).details.studentName)
    }

    @Test
    fun `fetchStudentDetails failure sets Error state`() {
        val student = LinkedStudent("id123", "أحمد محمد", "token123")
        every { repository.getLinkedStudents() } returns listOf(student)
        every { repository.getActiveStudent() } returns student
        coEvery { repository.getStudentDetails("id123") } returns Result.failure(Exception("Network error"))

        viewModel = DashboardViewModel(repository)
        testDispatcher.scheduler.advanceUntilIdle()

        assertTrue(viewModel.uiState.value is DashboardUiState.Error)
        assertEquals("Network error", (viewModel.uiState.value as DashboardUiState.Error).message)
    }

    @Test
    fun `removeStudent calls repository and reloads list`() {
        val student = LinkedStudent("id123", "أحمد محمد", "token123")
        every { repository.getLinkedStudents() } returnsMany listOf(listOf(student), emptyList())
        every { repository.getActiveStudent() } returnsMany listOf(student, null)
        coEvery { repository.getStudentDetails("id123") } returns Result.success(mockDetails)

        viewModel = DashboardViewModel(repository)
        testDispatcher.scheduler.advanceUntilIdle()

        viewModel.removeStudent("id123")
        testDispatcher.scheduler.advanceUntilIdle()

        verify { repository.removeLinkedStudent("id123") }
        assertEquals(0, viewModel.linkedStudents.value.size)
        assertEquals(null, viewModel.activeStudent.value)
        assertEquals(DashboardUiState.Empty, viewModel.uiState.value)
    }
}
