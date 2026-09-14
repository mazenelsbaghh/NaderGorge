package com.nadergorge.paymentlistener.service

import android.content.Context
import com.nadergorge.paymentlistener.data.api.ApiClient
import com.nadergorge.paymentlistener.data.api.ApiService
import com.nadergorge.paymentlistener.data.api.SyncStatusRequest
import com.nadergorge.paymentlistener.data.preference.PreferenceManager
import com.nadergorge.paymentlistener.data.preference.WalletPairing
import com.nadergorge.paymentlistener.data.preference.WalletSyncSnapshot
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.async
import kotlinx.coroutines.awaitAll
import kotlinx.coroutines.supervisorScope

internal enum class WalletStatusSyncOutcome {
    SUCCESS,
    RETRYABLE_FAILURE,
    CONFIGURATION_FAILURE
}

internal data class WalletStatusSyncSummary(
    val attemptedCount: Int,
    val successfulCount: Int,
    val retryableFailureCount: Int,
    val configurationFailureCount: Int
) {
    companion object {
        fun from(outcomes: List<WalletStatusSyncOutcome>): WalletStatusSyncSummary =
            WalletStatusSyncSummary(
                attemptedCount = outcomes.size,
                successfulCount = outcomes.count { it == WalletStatusSyncOutcome.SUCCESS },
                retryableFailureCount = outcomes.count {
                    it == WalletStatusSyncOutcome.RETRYABLE_FAILURE
                },
                configurationFailureCount = outcomes.count {
                    it == WalletStatusSyncOutcome.CONFIGURATION_FAILURE
                }
            )
    }
}

internal suspend fun syncEachWallet(
    pairings: List<WalletPairing>,
    syncPairing: suspend (WalletPairing) -> WalletStatusSyncOutcome
): List<WalletStatusSyncOutcome> = supervisorScope {
    pairings.map { pairing ->
        async { syncPairing(pairing) }
    }.awaitAll()
}

internal object WalletStatusSynchronizer {
    suspend fun syncAll(context: Context): WalletStatusSyncSummary {
        val preferences = PreferenceManager(context)
        val pairings = preferences.getWalletPairings()
        if (pairings.isEmpty()) return WalletStatusSyncSummary.from(emptyList())

        val apiService = ApiClient.getApiService(context)
            ?: return WalletStatusSyncSummary.from(
                List(pairings.size) { WalletStatusSyncOutcome.CONFIGURATION_FAILURE }
            )

        val outcomes = syncEachWallet(pairings) { pairing ->
            syncPairing(apiService, preferences, pairing)
        }
        return WalletStatusSyncSummary.from(outcomes)
    }

    private suspend fun syncPairing(
        apiService: ApiService,
        preferences: PreferenceManager,
        pairing: WalletPairing
    ): WalletStatusSyncOutcome = try {
        val response = apiService.syncStatus(pairing.token, SyncStatusRequest(null))
        val body = response.body()
        val data = body?.data

        when {
            response.isSuccessful && body?.success == true && data != null -> {
                preferences.updateWalletSync(
                    pairing.token,
                    WalletSyncSnapshot(
                        phoneNumber = data.phoneNumber,
                        label = data.label,
                        smsSenderFilters = data.smsSenderFilters,
                        currentBalance = data.currentBalance,
                        dailyLimit = data.dailyLimit,
                        monthlyLimit = data.monthlyLimit,
                        dailyReceived = data.dailyReceived,
                        monthlyReceived = data.monthlyReceived,
                        isActive = data.isActive
                    )
                )
                WalletStatusSyncOutcome.SUCCESS
            }
            PairingTokenFailurePolicy.isInvalid(body?.message) ||
                PairingTokenFailurePolicy.isInvalid(response.errorBody()?.string()) ||
                PairingTokenFailurePolicy.isPermanentClientFailure(response.code()) ->
                WalletStatusSyncOutcome.CONFIGURATION_FAILURE
            else -> WalletStatusSyncOutcome.RETRYABLE_FAILURE
        }
    } catch (error: CancellationException) {
        throw error
    } catch (_: Exception) {
        WalletStatusSyncOutcome.RETRYABLE_FAILURE
    }
}
