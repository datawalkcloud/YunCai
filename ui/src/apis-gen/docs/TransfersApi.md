# TransfersApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdTransfersGet**](TransfersApi.md#accountsetsaccountsetidtransfersget) | **GET** /accountsets/{accountSetId}/transfers |  |
| [**accountsetsAccountSetIdTransfersIdDelete**](TransfersApi.md#accountsetsaccountsetidtransfersiddelete) | **DELETE** /accountsets/{accountSetId}/transfers/{id} |  |
| [**accountsetsAccountSetIdTransfersIdPut**](TransfersApi.md#accountsetsaccountsetidtransfersidput) | **PUT** /accountsets/{accountSetId}/transfers/{id} |  |
| [**accountsetsAccountSetIdTransfersIdVoucherPost**](TransfersApi.md#accountsetsaccountsetidtransfersidvoucherpost) | **POST** /accountsets/{accountSetId}/transfers/{id}/voucher |  |
| [**accountsetsAccountSetIdTransfersPost**](TransfersApi.md#accountsetsaccountsetidtransferspost) | **POST** /accountsets/{accountSetId}/transfers |  |



## accountsetsAccountSetIdTransfersGet

> Array&lt;TransferDto&gt; accountsetsAccountSetIdTransfersGet(accountSetId)



### Example

```ts
import {
  Configuration,
  TransfersApi,
} from '';
import type { AccountsetsAccountSetIdTransfersGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TransfersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdTransfersGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdTransfersGet(body);
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

### Return type

[**Array&lt;TransferDto&gt;**](TransferDto.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: Not defined
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## accountsetsAccountSetIdTransfersIdDelete

> accountsetsAccountSetIdTransfersIdDelete(accountSetId, id)



### Example

```ts
import {
  Configuration,
  TransfersApi,
} from '';
import type { AccountsetsAccountSetIdTransfersIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TransfersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdTransfersIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdTransfersIdDelete(body);
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
| **id** | `string` |  | [Defaults to `undefined`] |

### Return type

`void` (Empty response body)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: Not defined
- **Accept**: Not defined


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## accountsetsAccountSetIdTransfersIdPut

> TransferDto accountsetsAccountSetIdTransfersIdPut(accountSetId, id, saveTransferRequest)



### Example

```ts
import {
  Configuration,
  TransfersApi,
} from '';
import type { AccountsetsAccountSetIdTransfersIdPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TransfersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveTransferRequest
    saveTransferRequest: ...,
  } satisfies AccountsetsAccountSetIdTransfersIdPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdTransfersIdPut(body);
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
| **id** | `string` |  | [Defaults to `undefined`] |
| **saveTransferRequest** | [SaveTransferRequest](SaveTransferRequest.md) |  | |

### Return type

[**TransferDto**](TransferDto.md)

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


## accountsetsAccountSetIdTransfersIdVoucherPost

> GenerateVoucherResultDto accountsetsAccountSetIdTransfersIdVoucherPost(accountSetId, id)



### Example

```ts
import {
  Configuration,
  TransfersApi,
} from '';
import type { AccountsetsAccountSetIdTransfersIdVoucherPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TransfersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdTransfersIdVoucherPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdTransfersIdVoucherPost(body);
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
| **id** | `string` |  | [Defaults to `undefined`] |

### Return type

[**GenerateVoucherResultDto**](GenerateVoucherResultDto.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: Not defined
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## accountsetsAccountSetIdTransfersPost

> TransferDto accountsetsAccountSetIdTransfersPost(accountSetId, saveTransferRequest)



### Example

```ts
import {
  Configuration,
  TransfersApi,
} from '';
import type { AccountsetsAccountSetIdTransfersPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TransfersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveTransferRequest
    saveTransferRequest: ...,
  } satisfies AccountsetsAccountSetIdTransfersPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdTransfersPost(body);
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
| **saveTransferRequest** | [SaveTransferRequest](SaveTransferRequest.md) |  | |

### Return type

[**TransferDto**](TransferDto.md)

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

