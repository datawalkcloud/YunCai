# VouchersApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdVouchersExportGet**](VouchersApi.md#accountsetsaccountsetidvouchersexportget) | **GET** /accountsets/{accountSetId}/Vouchers/export |  |
| [**accountsetsAccountSetIdVouchersGet**](VouchersApi.md#accountsetsaccountsetidvouchersget) | **GET** /accountsets/{accountSetId}/Vouchers |  |
| [**accountsetsAccountSetIdVouchersIdDelete**](VouchersApi.md#accountsetsaccountsetidvouchersiddelete) | **DELETE** /accountsets/{accountSetId}/Vouchers/{id} |  |
| [**accountsetsAccountSetIdVouchersIdGet**](VouchersApi.md#accountsetsaccountsetidvouchersidget) | **GET** /accountsets/{accountSetId}/Vouchers/{id} |  |
| [**accountsetsAccountSetIdVouchersIdPostPost**](VouchersApi.md#accountsetsaccountsetidvouchersidpostpost) | **POST** /accountsets/{accountSetId}/Vouchers/{id}/post |  |
| [**accountsetsAccountSetIdVouchersIdPut**](VouchersApi.md#accountsetsaccountsetidvouchersidput) | **PUT** /accountsets/{accountSetId}/Vouchers/{id} |  |
| [**accountsetsAccountSetIdVouchersIdReviewPost**](VouchersApi.md#accountsetsaccountsetidvouchersidreviewpost) | **POST** /accountsets/{accountSetId}/Vouchers/{id}/review |  |
| [**accountsetsAccountSetIdVouchersIdUnreviewPost**](VouchersApi.md#accountsetsaccountsetidvouchersidunreviewpost) | **POST** /accountsets/{accountSetId}/Vouchers/{id}/unreview |  |
| [**accountsetsAccountSetIdVouchersImportPost**](VouchersApi.md#accountsetsaccountsetidvouchersimportpost) | **POST** /accountsets/{accountSetId}/Vouchers/import |  |
| [**accountsetsAccountSetIdVouchersPost**](VouchersApi.md#accountsetsaccountsetidvoucherspost) | **POST** /accountsets/{accountSetId}/Vouchers |  |



## accountsetsAccountSetIdVouchersExportGet

> accountsetsAccountSetIdVouchersExportGet(accountSetId)



### Example

```ts
import {
  Configuration,
  VouchersApi,
} from '';
import type { AccountsetsAccountSetIdVouchersExportGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VouchersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdVouchersExportGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchersExportGet(body);
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


## accountsetsAccountSetIdVouchersGet

> Array&lt;VoucherListItemDto&gt; accountsetsAccountSetIdVouchersGet(accountSetId, startDate, endDate)



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
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
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
| **startDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **endDate** | `Date` |  | [Optional] [Defaults to `undefined`] |

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


## accountsetsAccountSetIdVouchersIdPostPost

> VoucherDetailDto accountsetsAccountSetIdVouchersIdPostPost(accountSetId, id)



### Example

```ts
import {
  Configuration,
  VouchersApi,
} from '';
import type { AccountsetsAccountSetIdVouchersIdPostPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VouchersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdVouchersIdPostPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchersIdPostPost(body);
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


## accountsetsAccountSetIdVouchersIdReviewPost

> VoucherDetailDto accountsetsAccountSetIdVouchersIdReviewPost(accountSetId, id)



### Example

```ts
import {
  Configuration,
  VouchersApi,
} from '';
import type { AccountsetsAccountSetIdVouchersIdReviewPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VouchersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdVouchersIdReviewPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchersIdReviewPost(body);
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


## accountsetsAccountSetIdVouchersIdUnreviewPost

> VoucherDetailDto accountsetsAccountSetIdVouchersIdUnreviewPost(accountSetId, id)



### Example

```ts
import {
  Configuration,
  VouchersApi,
} from '';
import type { AccountsetsAccountSetIdVouchersIdUnreviewPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VouchersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdVouchersIdUnreviewPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchersIdUnreviewPost(body);
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


## accountsetsAccountSetIdVouchersImportPost

> VoucherImportResult accountsetsAccountSetIdVouchersImportPost(accountSetId, file, conflict)



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
    // number (optional)
    conflict: 56,
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
| **conflict** | `number` |  | [Optional] [Defaults to `undefined`] |

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

