package library.slvz.myketbridge

import android.content.Context
import android.content.Intent
import androidx.appcompat.app.AppCompatActivity
import ir.myket.billingclient.IabHelper
import ir.myket.billingclient.util.IabResult
import ir.myket.billingclient.util.Inventory
import ir.myket.billingclient.util.Purchase
import org.json.JSONObject


object MyketBridge {
    private var helper: IabHelper? = null
    private var isSetup = false
    private var _context: Context? = null

    // ============================
    // Initialization
    // ============================
    @JvmStatic
    public fun Initialize(context: Context, publicKey: String) {
        _context = context
        helper = IabHelper(context, publicKey)
        helper?.startSetup { result ->
            isSetup = result.isSuccess
            var type = "init_";
            if(result.isSuccess)
                type = type + "success"
            else
                type = type + "error"
            sendResult(
                type,
                result.message
            )
        }
    }

    @JvmStatic
    // ============================
    // Purchase Product
    // ============================
    public fun Purchase(sku: String, payload: String?) {
        if (!isSetup) {
            sendResult(
                "purchase_failed",
                "Myket not initialized"
            )
            return
        }

        try {
            helper?.launchPurchaseFlow(_context as AppCompatActivity,sku,object : IabHelper.OnIabPurchaseFinishedListener{
                override fun onIabPurchaseFinished(
                    result: IabResult?,
                    info: Purchase?
                ) {
                    val map = mapOf(
                        "productId" to info?.sku,
                        "purchaseToken" to info?.token,
                        "state" to info?.purchaseState,
                        "payload" to info?.developerPayload,
                        "signature" to info?.signature,
                        "orderId" to info?.orderId,
                        "originalJson" to info?.originalJson,
                        "purchaseTime" to info?.purchaseTime,
                        "type" to info?.itemType
                    )
                    val json = JSONObject(map).toString()

                    var type = "purchase_";
                    if(result!!.isSuccess)
                        type = type + "success"
                    else
                        type = type + "failed"
                    sendResult(
                        type,
                        result.message,
                        json
                    )

                }
            })

        } catch (e: Exception) {
            sendResult(
                "purchase_failed",
                e.message ?: "purchase exception"
            )
        }
    }


    @JvmStatic
    // ============================
    // Subscribe Product
    // ============================
    public fun Subscribe(sku: String, payload: String?) {
        if (!isSetup) {
            sendResult(
                "subscribe_failed",
                "Myket not initialized"
            )
            return
        }

        try {
            helper?.launchSubscriptionPurchaseFlow(_context as AppCompatActivity,sku,object : IabHelper.OnIabPurchaseFinishedListener{
                override fun onIabPurchaseFinished(
                    result: IabResult?,
                    info: Purchase?
                ) {
                    val map = mapOf(
                        "productId" to info?.sku,
                        "purchaseToken" to info?.token,
                        "state" to info?.purchaseState,
                        "payload" to info?.developerPayload,
                        "signature" to info?.signature,
                        "orderId" to info?.orderId,
                        "originalJson" to info?.originalJson,
                        "purchaseTime" to info?.purchaseTime,
                        "type" to info?.itemType
                    )
                    val json = JSONObject(map).toString()
                    var type = "subscribe_";
                    if(result!!.isSuccess)
                        type = type + "success"
                    else
                        type = type + "failed"
                    sendResult(
                        type,
                        result.message,
                        json
                    )
                }
            })

        } catch (e: Exception) {
            sendResult(
                "subscribe_failed",
                e.message ?: "subscribe exception"
            )
        }
    }


    @JvmStatic
    // ============================
    // Consume Product
    // ============================
    public fun Consume(itemType: String,jsonPurchaseInfo:String,signature:String) {
        if (!isSetup) {
            sendResult(
                "consume_failed",
                "Myket not initialized")
            return
        }

        try {

            val purchase: Purchase
            purchase = Purchase(itemType,jsonPurchaseInfo,signature);

            helper?.consumeAsync(purchase, object : IabHelper.OnConsumeFinishedListener{
                override fun onConsumeFinished(
                    purchase: Purchase?,
                    result: IabResult?
                ) {
                    var type = "consume_";
                    if(result!!.isSuccess)
                        type = type + "success"
                    else
                        type = type + "failed"
                    sendResult(
                        type,
                        result.message
                    )
                }
            })

        } catch (e: Exception) {
            sendResult(
                "consume_failed",
                e.message ?: "consume exception"
            )
        }
    }


    @JvmStatic
    // ============================
    // All Availible Items
    // ============================
    public fun AllAvailibleItems(skus: List<String>) {
        if (!isSetup) {
            sendResult(
                "query_failed",
                "Myket not initialized"
            )
            return
        }

        try {
            helper?.queryInventoryAsync(true,skus, object : IabHelper.QueryInventoryFinishedListener{
                override fun onQueryInventoryFinished(
                    result: IabResult?,
                    inv: Inventory?
                ) {
                    val list = inv!!.allProducts.map { info ->
                        JSONObject(
                            mapOf(
                                "productId" to info?.sku,
                                "description" to info?.description,
                                "price" to info?.price,
                                "title" to info?.title,
                                "type" to info?.type,
                            )
                        )
                    }
                    val jsonList = org.json.JSONArray(list).toString()
                    var type = "query_";
                    if(result!!.isSuccess)
                        type = type + "success"
                    else
                        type = type + "failed"
                    sendResult(
                        type,
                        result.message,
                        jsonList
                    )
                }
            })

        } catch (e: Exception) {
            sendResult(
                "query_failed",
                e.message ?: "query exception"
            )
        }
    }


    private fun sendResult(type: String, message: String = "", product: String = "") {
        val intent = Intent("library.slvz.myketbridge.RESULT")

        intent.putExtra("result_type", type)
        intent.putExtra("result_message", message)
        intent.putExtra("result_product", product)

        _context?.sendBroadcast(intent)
    }


}