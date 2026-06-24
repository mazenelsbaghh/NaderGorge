package com.nadergorge.parent

import com.nadergorge.parent.data.repository.ParentRepository
import com.nadergorge.parent.data.storage.LinkedStudent
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
    fun `onCodeChange limits characters to 6 and forces uppercase`() {
        viewModel.onCodeChange("abc")
        assertEquals("ABC", viewModel.code.value)

        viewModel.onCodeChange("abcdefg")
        assertEquals("ABC", viewModel.code.value) // shouldn't update if length > 6

        viewModel.onCodeChange("abcdef")
        assertEquals("ABCDEF", viewModel.code.value)
    }

    @Test
    fun `linkStudent with invalid code length sets Error state`() {
        viewModel.onCodeChange("abc")
        viewModel.linkStudent("token")
        
        testDispatcher.scheduler.advanceUntilIdle()
        
        assertTrue(viewModel.uiState.value is LinkingUiState.Error)
        assertEquals("الرمز غير صالح، يجب أن يتكون من 6 خانات", (viewModel.uiState.value as LinkingUiState.Error).message)
        coVerify(exactly = 0) { repository.verifyAndLink(any(), any()) }
    }

    @Test
    fun `linkStudent success updates state to Success`() {
        val student = LinkedStudent("id123", "أحمد محمد", "token123")
        coEvery { repository.verifyAndLink("NG79F4", "token") } returns Result.success(student)

        viewModel.onCodeChange("ng79f4")
        viewModel.linkStudent("token")
        
        assertEquals(LinkingUiState.Loading, viewModel.uiState.value)
        
        testDispatcher.scheduler.advanceUntilIdle()
        
        assertEquals(LinkingUiState.Success("أحمد محمد"), viewModel.uiState.value)
    }

    @Test
    fun `linkStudent failure updates state to Error`() {
        coEvery { repository.verifyAndLink("NG79F4", "token") } returns Result.failure(Exception("Invalid code"))

        viewModel.onCodeChange("ng79f4")
        viewModel.linkStudent("token")
        
        assertEquals(LinkingUiState.Loading, viewModel.uiState.value)
        
        testDispatcher.scheduler.advanceUntilIdle()
        
        assertTrue(viewModel.uiState.value is LinkingUiState.Error)
        assertEquals("الرمز غير صالح، يرجى التحقق وإعادة المحاولة", (viewModel.uiState.value as LinkingUiState.Error).message)
    }
}
