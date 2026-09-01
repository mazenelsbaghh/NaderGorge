package com.nadergorge.paymentlistener.data.preference

data class WalletPairing(
    val token: String,
    val subscriptionId: Int? = null,
    val simSlotIndex: Int? = null,
    val simLabel: String? = null,
    val carrierLabel: String? = null,
    val phoneNumber: String = "",
    val label: String = "",
    val smsSenderFilters: List<String> = emptyList(),
    val currentBalance: Double = 0.0,
    val dailyLimit: Double = 0.0,
    val monthlyLimit: Double = 0.0,
    val dailyReceived: Double = 0.0,
    val monthlyReceived: Double = 0.0,
    val isActive: Boolean = true
) {
    fun simBinding(): WalletSimBinding = WalletSimBinding(
        subscriptionId = subscriptionId,
        simSlotIndex = simSlotIndex,
        simLabel = simLabel,
        carrierLabel = carrierLabel
    )

    override fun toString(): String =
        "WalletPairing(token=***, subscriptionId=$subscriptionId, simSlotIndex=$simSlotIndex, " +
            "simLabel=$simLabel, carrierLabel=$carrierLabel, label=$label, isActive=$isActive)"
}

data class WalletSimBinding(
    val subscriptionId: Int?,
    val simSlotIndex: Int?,
    val simLabel: String?,
    val carrierLabel: String?
) {
    companion object {
        val Unassigned = WalletSimBinding(
            subscriptionId = null,
            simSlotIndex = null,
            simLabel = null,
            carrierLabel = null
        )
    }
}

data class WalletSyncSnapshot(
    val phoneNumber: String,
    val label: String,
    val smsSenderFilters: List<String>,
    val currentBalance: Double,
    val dailyLimit: Double,
    val monthlyLimit: Double,
    val dailyReceived: Double,
    val monthlyReceived: Double,
    val isActive: Boolean
)

enum class WalletPairingWriteResult {
    ADDED,
    UPDATED,
    UNCHANGED,
    TOKEN_CONFLICT,
    SIM_CONFLICT,
    NOT_FOUND
}
