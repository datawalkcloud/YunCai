# VouchersApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdVouchersGet**](VouchersApi.md#accountsetsaccountsetidvouchersget) | **GET** /accountsets/{accountSetId}/Vouchers |  |
| [**accountsetsAccountSetIdVouchersIdDelete**](VouchersApi.md#accountsetsaccountsetidvouchersiddelete) | **DELETE** /accountsets/{accountSetId}/Vouchers/{id} |  |
| [**accountsetsAccountSetIdVouchersIdGet**](VouchersApi.md#accountsetsaccountsetidvouchersidget) | **GET** /accountsets/{accountSetId}/Vouchers/{id} |  |
| [**accountsetsAccountSetIdVouchersIdPut**](VouchersApi.md#accountsetsaccountsetidvouchersidput) | **PUT** /accountsets/{accountSetId}/Vouchers/{id} |  |
| [**accountsetsAccountSetIdVouchersImportPost**](VouchersApi.md#accountsetsaccountsetidvouchersimportpost) | **POST** /accountsets/{accountSetId}/Vouchers/import |  |
| [**accountsetsAccountSetIdVouchersPost**](VouchersApi.md#accountsetsaccountsetidvoucherspost) | **POST** /accountsets/{accountSetId}/Vouchers |  |



## accountsetsAccountSetIdVouchersGet

> Array&lt;VoucherListItemDto&gt; accountsetsAccountSetIdVouchersGet(accountSetId)



### Example

```ts
import {
  Configuration,
  VouchersApi,
} from '';
import type { AccountsetsAccountSetIdVouchersGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VouchersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdVouchersGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchersGet(body);
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

[**Array&lt;VoucherListItemDto&gt;**](VoucherListItemDto.md)

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


## accountsetsAccountSetIdVouchersIdDelete

> accountsetsAccountSetIdVouchersIdDelete(accountSetId, id)



### Example

```ts
import {
  Configuration,
  VouchersApi,
} from '';
import type { AccountsetsAccountSetIdVouchersIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VouchersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdVouchersIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchersIdDelete(body);
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


## accountsetsAccountSetIdVouchersIdGet

> VoucherDetailDto accountsetsAccountSetIdVouchersIdGet(accountSetId, id)



### Example

```ts
import {
  Configuration,
  VouchersApi,
} from '';
import type { AccountsetsAccountSetIdVouchersIdGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VouchersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdVouchersIdGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchersIdGet(body);
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

[**VoucherDetailDto**](VoucherDetailDto.md)

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


## accountsetsAccountSetIdVouchersIdPut

> VoucherDetailDto accountsetsAccountSetIdVouchersIdPut(accountSetId, id, updateVoucherRequest)



### Example

```ts
import {
  Configuration,
  VouchersApi,
} from '';
import type { AccountsetsAccountSetIdVouchersIdPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VouchersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // UpdateVoucherRequest
    updateVoucherRequest: ...,
  } satisfies AccountsetsAccountSetIdVouchersIdPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchersIdPut(body);
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
| **updateVoucherRequest** | [UpdateVoucherRequest](UpdateVoucherRequest.md) |  | |

### Return type

[**VoucherDetailDto**](VoucherDetailDto.md)

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


## accountsetsAccountSetIdVouchersImportPost

> VoucherImportResult accountsetsAccountSetIdVouchersImportPost(accountSetId, file)



### Example

```ts
import {
  Configuration,
  VouchersApi,
} from '';
import type { AccountsetsAccountSetIdVouchersImportPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VouchersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Blob (optional)
    file: BINARY_DATA_HERE,
  } satisfies AccountsetsAccountSetIdVouchersImportPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchersImportPost(body);
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
| **file** | `Blob` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**VoucherImportResult**](VoucherImportResult.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: `multipart/form-data`
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## accountsetsAccountSetIdVouchersPost

> VoucherDetailDto accountsetsAccountSetIdVouchersPost(accountSetId, createVoucherRequest)



### Example

```ts
import {
  Configuration,
  VouchersApi,
} from '';
import type { AccountsetsAccountSetIdVouchersPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VouchersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // CreateVoucherRequest
    createVoucherRequest: ...,
  } satisfies AccountsetsAccountSetIdVouchersPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchersPost(body);
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
| **createVoucherRequest** | [CreateVoucherRequest](CreateVoucherRequest.md) |  | |

### Return type

[**VoucherDetailDto**](VoucherDetailDto.md)

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

