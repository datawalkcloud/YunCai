# ReinitApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdReinitPost**](ReinitApi.md#accountsetsaccountsetidreinitpost) | **POST** /accountsets/{accountSetId}/reinit |  |



## accountsetsAccountSetIdReinitPost

> ReinitResultDto accountsetsAccountSetIdReinitPost(accountSetId, wipeRequest)



### Example

```ts
import {
  Configuration,
  ReinitApi,
} from '';
import type { AccountsetsAccountSetIdReinitPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ReinitApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // WipeRequest
    wipeRequest: ...,
  } satisfies AccountsetsAccountSetIdReinitPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdReinitPost(body);
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
| **accountSetId** | `string` |  | [Defaults to `undefined`] |
| **wipeRequest** | [WipeRequest](WipeRequest.md) |  | |

### Return type

[**ReinitResultDto**](ReinitResultDto.md)

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

