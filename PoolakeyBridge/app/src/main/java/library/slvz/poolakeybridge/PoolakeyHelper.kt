package library.slvz.poolakeybridge

import android.content.Context
import android.content.Intent
import androidx.appcompat.app.AppCompatActivity
import ir.cafebazaar.poolakey.Payment
import ir.cafebazaar.poolakey.config.PaymentConfiguration
import ir.cafebazaar.poolakey.config.SecurityCheck
import ir.cafebazaar.poolakey.entity.PurchaseInfo
import ir.cafebazaar.poolakey.entity.PurchaseState
import ir.cafebazaar.poolakey.request.PurchaseRequest
import org.json.JSONObject
import kotlin.String


object PoolakeyHelper {

    private var _context: Context? = null
    private var rsaKey: String? = null
    private var payment: Payment? = null
    private var _activity: AppCompatActivity? = null

    @JvmStatic
    public fun Initialize(context: Context, _rsaKey: String) {
        _context = context
        rsaKey = _rsaKey

        _activity = context as AppCompatActivity

        rsaKey?.let {
            try {
                val config = PaymentConfiguration(localSecurityCheck = SecurityCheck.Enable(it))
                payment = Payment(context, config)
                payment?.connect {
                    connectionFailed { ex ->
                        sendResult("init_error", "Failed to connect to Bazaar")
                    }
                    connectionSucceed {
                        sendResult("init_success")
                    }
                }

            } catch (ex: Exception) {
                sendResult("init_error", ex.message ?: "init error")
            }
        }
    }

    @JvmStatic
    public fun Purchase(productId: String, payload: String, consumeAfter: Boolean) {
        val pay = payment ?: run {
            sendResult("purchase_failed", "payment not initialized")
            return
        }

        try {
            val req = PurchaseRequest(productId = productId, payload = payload)
            pay.purchaseProduct(
                registry = _activity!!.activityResultRegistry,
                request = req
            ) {
                failedToBeginFlow { ex ->
                    val msg = ex?.message ?: "failed to begin flow"
                    sendResult("purchase_failed", msg, product = productId)
                    // if dynamic price not supported, you could retry without dynamic token if you used one
                }
                purchaseSucceed { info: PurchaseInfo ->

                    val map = mapOf(
                        "productId" to info?.productId,
                        "purchaseToken" to info?.purchaseToken,
                        "state" to info?.purchaseState,
                        "payload" to info?.payload,
                        "signature" to info?.dataSignature,
                        "orderId" to info?.orderId,
                        "originalJson" to info?.originalJson,
                        "purchaseTime" to info?.purchaseTime,
                        "type" to "inapp"
                    )
                    val json = JSONObject(map).toString()

                    if (info.purchaseState == PurchaseState.PURCHASED) {
                        sendResult("purchase_success", "ok", json)
                        if (consumeAfter) {
                            // consume async
                            pay.consumeProduct(info.purchaseToken) {
                                consumeSucceed {
                                    sendResult("consume_success", "consumed", json)
                                }
                                consumeFailed { ex2 ->
                                    sendResult("consume_failed", ex2?.message ?: "consume fail", json)
                                }
                            }
                        }
                    } else {
                        sendResult("purchase_failed", "state=${info.purchaseState}", json)
                    }
                }
                purchaseCanceled {
                    sendResult("purchase_canceled", "user canceled",)
                }
                purchaseFailed { ex ->
                    sendResult("purchase_failed", ex?.message ?: "purchase failed")
                }
            }
        } catch (ex: Exception) {
            sendResult("purchase_failed", ex.message ?: "purchase exception", product = productId)
        }
    }

    @JvmStatic
    public fun Subscribe(productId: String, payload: String) {
        val pay = payment ?: run {
            sendResult("subscribe_failed", "payment not initialized")
            return
        }
        try {
            val req = PurchaseRequest(productId = productId, payload = payload)
            pay.subscribeProduct(
                registry = _activity!!.activityResultRegistry,
                request = req
            ) {
                failedToBeginFlow { ex ->
                    sendResult("subscribe_failed", ex?.message ?: "failed to begin subscribe")
                }
                purchaseSucceed { info ->
                    val map = mapOf(
                        "productId" to info?.productId,
                        "purchaseToken" to info?.purchaseToken,
                        "state" to info?.purchaseState,
                        "payload" to info?.payload,
                        "signature" to info?.dataSignature,
                        "orderId" to info?.orderId,
                        "originalJson" to info?.originalJson,
                        "purchaseTime" to info?.purchaseTime,
                        "type" to "subs"
                    )
                    val json = JSONObject(map).toString()
                    sendResult("subscribe_success", "ok", json)
                }
                purchaseCanceled {
                    sendResult("subscribe_canceled", "user canceled")
                }
                purchaseFailed { ex ->
                    sendResult("subscribe_failed", ex?.message ?: "subscribe failed")
                }
            }
        } catch (ex: Exception) {
            sendResult("subscribe_failed", ex.message ?: "subscribe exception")
        }
    }

    @JvmStatic
    public fun Consume(purchaseToken: String, productId: String) {
        val pay = payment ?: run {
            sendResult("consume_failed", "payment not initialized")
            return
        }
        try {
            pay.consumeProduct(purchaseToken) {
                consumeSucceed {
                    sendResult("consume_success", "consumed", product = productId)
                }
                consumeFailed { ex ->
                    sendResult("consume_failed", ex?.message ?: "consume failed", product = productId)
                }
            }
        } catch (ex: Exception) {
            sendResult("consume_failed", ex.message ?: "consume exception", product = productId)
        }
    }

    @JvmStatic
    public fun AllAvailibleInappItems(skus: List<String>) {
        val pay = payment ?: run {
            sendResult("query_failed", "{\"error\":\"payment not initialized\"}")
            return
        }

        try {
            pay.getInAppSkuDetails(skus){
                getSkuDetailsSucceed { result ->
                    val list = result.map { info ->
                        JSONObject(
                            mapOf(
                                "productId" to info?.sku,
                                "description" to info?.description,
                                "price" to info?.price,
                                "title" to info?.title,
                                "type" to info?.type
                            )
                        )
                    }
                    val jsonList = org.json.JSONArray(list).toString()

                    sendResult("query_success", "Ok",jsonList)
                }
                getSkuDetailsFailed { result ->
                    sendResult("query_failed", result.message ?: "query exception")
                }
            }

            pay.getSubscribedProducts {
                querySucceed { subscribedList ->
                    val list = subscribedList.map { info ->
                        JSONObject(
                            mapOf(
                                "productId" to info.productId,
                                "purchaseToken" to info.purchaseToken,
                                "state" to info.purchaseState.name,
                                "payload" to info.payload,
                                "dataSignature" to info.dataSignature,
                                "orderId" to info.orderId,
                            )
                        )
                    }
                    val jsonList = org.json.JSONArray(list).toString()

                    sendResult("all_subscription_success", "ok", jsonList)
                }
                queryFailed { ex ->
                    sendResult("all_subscription_failed", "Failed", )
                }
            }
        } catch (ex: Exception) {
            sendResult("query_failed", ex.message ?: "query exception")
        }
    }

    @JvmStatic
    public fun AllAvailibleSubItems(skus: List<String>) {
        val pay = payment ?: run {
            sendResult("query_failed", "{\"error\":\"payment not initialized\"}")
            return
        }

        try {

            pay.getSubscriptionSkuDetails(skus){
                getSkuDetailsSucceed { result ->
                    val list = result.map { info ->
                        JSONObject(
                            mapOf(
                                "productId" to info?.sku,
                                "description" to info?.description,
                                "price" to info?.price,
                                "title" to info?.title,
                                "type" to info?.type
                            )
                        )
                    }
                    val jsonList = org.json.JSONArray(list).toString()

                    sendResult("query_success", "Ok",jsonList)
                }
                getSkuDetailsFailed { result ->
                    sendResult("query_failed", result.message ?: "query exception")
                }
            }

            pay.getSubscribedProducts {
                querySucceed { subscribedList ->
                    val list = subscribedList.map { info ->
                        JSONObject(
                            mapOf(
                                "productId" to info.productId,
                                "purchaseToken" to info.purchaseToken,
                                "state" to info.purchaseState.name,
                                "payload" to info.payload,
                                "dataSignature" to info.dataSignature,
                                "orderId" to info.orderId,
                            )
                        )
                    }
                    val jsonList = org.json.JSONArray(list).toString()

                    sendResult("all_subscription_success", "ok", jsonList)
                }
                queryFailed { ex ->
                    sendResult("all_subscription_failed", "Failed", )
                }
            }
        } catch (ex: Exception) {
            sendResult("query_failed", ex.message ?: "query exception")
        }
    }

    @JvmStatic
    public fun SubscriptionCheck() {
        val pay = payment ?: run {
            sendResult("subscription_check_failed", "{\"error\":\"payment not initialized\"}")
            return
        }

        try {
            pay.getSubscribedProducts {
                querySucceed { subscribedList ->
                    val list = subscribedList.map { info ->
                        JSONObject(
                            mapOf(
                                "productId" to info?.productId,
                                "purchaseToken" to info?.purchaseToken,
                                "state" to info?.purchaseState,
                                "payload" to info?.payload,
                                "signature" to info?.dataSignature,
                                "orderId" to info?.orderId,
                                "originalJson" to info?.originalJson,
                                "purchaseTime" to info?.purchaseTime,
                                "type" to "bubs"
                            )
                        )
                    }
                    val jsonList = org.json.JSONArray(list).toString()

                    sendResult("all_subscription_success", "ok", jsonList)
                }
                queryFailed { ex ->
                    sendResult("all_subscription_failed", "Failed", )
                }
            }
        } catch (ex: Exception) {
            sendResult("all_subscription_failed", ex.message ?: "check exception")
        }
    }

    @JvmStatic
    public fun PurchasesCheck() {
        val pay = payment ?: run {
            sendResult("purchase_check_failed", "{\"error\":\"payment not initialized\"}")
            return
        }

        try {
            pay.getPurchasedProducts {
                querySucceed { purchasedList ->
                    val list = purchasedList.map { info ->
                        JSONObject(
                            mapOf(
                                "productId" to info?.productId,
                                "purchaseToken" to info?.purchaseToken,
                                "state" to info?.purchaseState,
                                "payload" to info?.payload,
                                "signature" to info?.dataSignature,
                                "orderId" to info?.orderId,
                                "originalJson" to info?.originalJson,
                                "purchaseTime" to info?.purchaseTime,
                                "type" to "inapp"
                            )
                        )
                    }
                    val jsonList = org.json.JSONArray(list).toString()
                    sendResult("all_purchase_success", "ok", jsonList)
                }
                queryFailed { ex ->
                    sendResult("all_purchase_failed", "Failed")
                }
            }
        } catch (ex: Exception) {
            sendResult("all_purchase_failed", ex.message.toString())
        }
    }


    private fun sendResult(type: String, message: String = "", product: String = "") {
        val intent = Intent("library.slvz.poolakeybridge.RESULT")

        intent.putExtra("result_type", type)
        intent.putExtra("result_message", message)
        intent.putExtra("result_product", product)

        _context?.sendBroadcast(intent)
    }

}
