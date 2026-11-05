# TaxLoginTicketApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**taxLoginTicketRedeemPost**](TaxLoginTicketApi.md#taxloginticketredeempost) | **POST** /TaxLoginTicket/redeem |  |



## taxLoginTicketRedeemPost

> TaxLoginPasswordDto taxLoginTicketRedeemPost(taxLoginTicketRedeemRequest)



### Example

```ts
import {
  Configuration,
  TaxLoginTicketApi,
} from '';
import type { TaxLoginTicketRedeemPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxLoginTicketApi();

  const body = {
    // TaxLoginTicketRedeemRequest
    taxLoginTicketRedeemRequest: ...,
  } satisfies TaxLoginTicketRedeemPostRequest;

  try {
    const data = await api.taxLoginTicketRedeemPost(body);
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
| **taxLoginTicketRedeemRequest** | [TaxLoginTicketRedeemRequest](TaxLoginTicketRedeemRequest.md) |  | |

### Return type

[**TaxLoginPasswordDto**](TaxLoginPasswordDto.md)

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

