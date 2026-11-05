# TaxPortalBrowserSessionApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**taxPortalBrowserSessionRedeemPost**](TaxPortalBrowserSessionApi.md#taxportalbrowsersessionredeempost) | **POST** /TaxPortalBrowserSession/redeem |  |



## taxPortalBrowserSessionRedeemPost

> TaxPortalBrowserHandoffDto taxPortalBrowserSessionRedeemPost(taxPortalBrowserTicketRedeemRequest)



### Example

```ts
import {
  Configuration,
  TaxPortalBrowserSessionApi,
} from '';
import type { TaxPortalBrowserSessionRedeemPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalBrowserSessionApi();

  const body = {
    // TaxPortalBrowserTicketRedeemRequest
    taxPortalBrowserTicketRedeemRequest: ...,
  } satisfies TaxPortalBrowserSessionRedeemPostRequest;

  try {
    const data = await api.taxPortalBrowserSessionRedeemPost(body);
    console.log(data);
  } catch (error) {
    console.error(error);
  }
}

// Run the test
example().catch(console.error);
```

### Parameters


| Name | Type | Description  | Notes |
|------------- | ------------- | ------------- | -------------|
| **taxPortalBrowserTicketRedeemRequest** | [TaxPortalBrowserTicketRedeemRequest](TaxPortalBrowserTicketRedeemRequest.md) |  | |

### Return type

[**TaxPortalBrowserHandoffDto**](TaxPortalBrowserHandoffDto.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: `application/json`, `text/json`, `application/*+json`
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)

