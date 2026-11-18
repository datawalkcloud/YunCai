# VoucherWordsApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdVoucherwordsGet**](VoucherWordsApi.md#accountsetsaccountsetidvoucherwordsget) | **GET** /accountsets/{accountSetId}/voucherwords |  |
| [**accountsetsAccountSetIdVoucherwordsIdDelete**](VoucherWordsApi.md#accountsetsaccountsetidvoucherwordsiddelete) | **DELETE** /accountsets/{accountSetId}/voucherwords/{id} |  |
| [**accountsetsAccountSetIdVoucherwordsIdPut**](VoucherWordsApi.md#accountsetsaccountsetidvoucherwordsidput) | **PUT** /accountsets/{accountSetId}/voucherwords/{id} |  |
| [**accountsetsAccountSetIdVoucherwordsPost**](VoucherWordsApi.md#accountsetsaccountsetidvoucherwordspost) | **POST** /accountsets/{accountSetId}/voucherwords |  |



## accountsetsAccountSetIdVoucherwordsGet

> Array&lt;VoucherWordDto&gt; accountsetsAccountSetIdVoucherwordsGet(accountSetId)



### Example

```ts
import {
  Configuration,
  VoucherWordsApi,
} from '';
import type { AccountsetsAccountSetIdVoucherwordsGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VoucherWordsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdVoucherwordsGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdVoucherwordsGet(body);
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

[**Array&lt;VoucherWordDto&gt;**](VoucherWordDto.md)

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


## accountsetsAccountSetIdVoucherwordsIdDelete

> accountsetsAccountSetIdVoucherwordsIdDelete(accountSetId, id)



### Example

```ts
import {
  Configuration,
  VoucherWordsApi,
} from '';
import type { AccountsetsAccountSetIdVoucherwordsIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VoucherWordsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdVoucherwordsIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdVoucherwordsIdDelete(body);
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


## accountsetsAccountSetIdVoucherwordsIdPut

> VoucherWordDto accountsetsAccountSetIdVoucherwordsIdPut(accountSetId, id, saveVoucherWordRequest)



### Example

```ts
import {
  Configuration,
  VoucherWordsApi,
} from '';
import type { AccountsetsAccountSetIdVoucherwordsIdPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VoucherWordsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveVoucherWordRequest
    saveVoucherWordRequest: ...,
  } satisfies AccountsetsAccountSetIdVoucherwordsIdPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdVoucherwordsIdPut(body);
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
| **saveVoucherWordRequest** | [SaveVoucherWordRequest](SaveVoucherWordRequest.md) |  | |

### Return type

[**VoucherWordDto**](VoucherWordDto.md)

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


## accountsetsAccountSetIdVoucherwordsPost

> VoucherWordDto accountsetsAccountSetIdVoucherwordsPost(accountSetId, saveVoucherWordRequest)



### Example

```ts
import {
  Configuration,
  VoucherWordsApi,
} from '';
import type { AccountsetsAccountSetIdVoucherwordsPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VoucherWordsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveVoucherWordRequest
    saveVoucherWordRequest: ...,
  } satisfies AccountsetsAccountSetIdVoucherwordsPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdVoucherwordsPost(body);
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
| **saveVoucherWordRequest** | [SaveVoucherWordRequest](SaveVoucherWordRequest.md) |  | |

### Return type

[**VoucherWordDto**](VoucherWordDto.md)

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

