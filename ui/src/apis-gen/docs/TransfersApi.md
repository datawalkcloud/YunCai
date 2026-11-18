# TransfersApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdTransfersBulkDeletePost**](TransfersApi.md#accountsetsaccountsetidtransfersbulkdeletepost) | **POST** /accountsets/{accountSetId}/transfers/bulk-delete |  |
| [**accountsetsAccountSetIdTransfersExportGet**](TransfersApi.md#accountsetsaccountsetidtransfersexportget) | **GET** /accountsets/{accountSetId}/transfers/export |  |
| [**accountsetsAccountSetIdTransfersGet**](TransfersApi.md#accountsetsaccountsetidtransfersget) | **GET** /accountsets/{accountSetId}/transfers |  |
| [**accountsetsAccountSetIdTransfersIdDelete**](TransfersApi.md#accountsetsaccountsetidtransfersiddelete) | **DELETE** /accountsets/{accountSetId}/transfers/{id} |  |
| [**accountsetsAccountSetIdTransfersIdPut**](TransfersApi.md#accountsetsaccountsetidtransfersidput) | **PUT** /accountsets/{accountSetId}/transfers/{id} |  |
| [**accountsetsAccountSetIdTransfersIdVoucherPost**](TransfersApi.md#accountsetsaccountsetidtransfersidvoucherpost) | **POST** /accountsets/{accountSetId}/transfers/{id}/voucher |  |
| [**accountsetsAccountSetIdTransfersImportPost**](TransfersApi.md#accountsetsaccountsetidtransfersimportpost) | **POST** /accountsets/{accountSetId}/transfers/import |  |
| [**accountsetsAccountSetIdTransfersImportTemplateGet**](TransfersApi.md#accountsetsaccountsetidtransfersimporttemplateget) | **GET** /accountsets/{accountSetId}/transfers/import/template |  |
| [**accountsetsAccountSetIdTransfersPdfGet**](TransfersApi.md#accountsetsaccountsetidtransferspdfget) | **GET** /accountsets/{accountSetId}/transfers/pdf |  |
| [**accountsetsAccountSetIdTransfersPost**](TransfersApi.md#accountsetsaccountsetidtransferspost) | **POST** /accountsets/{accountSetId}/transfers |  |
| [**accountsetsAccountSetIdTransfersVouchersPost**](TransfersApi.md#accountsetsaccountsetidtransfersvoucherspost) | **POST** /accountsets/{accountSetId}/transfers/vouchers |  |



## accountsetsAccountSetIdTransfersBulkDeletePost

> number accountsetsAccountSetIdTransfersBulkDeletePost(accountSetId, bulkTransferRequest)



### Example

```ts
import {
  Configuration,
  TransfersApi,
} from '';
import type { AccountsetsAccountSetIdTransfersBulkDeletePostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TransfersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // BulkTransferRequest
    bulkTransferRequest: ...,
  } satisfies AccountsetsAccountSetIdTransfersBulkDeletePostRequest;

  try {
    const data = await api.accountsetsAccountSetIdTransfersBulkDeletePost(body);
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
| **bulkTransferRequest** | [BulkTransferRequest](BulkTransferRequest.md) |  | |

### Return type

**number**

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


## accountsetsAccountSetIdTransfersExportGet

> accountsetsAccountSetIdTransfersExportGet(accountSetId)



### Example

```ts
import {
  Configuration,
  TransfersApi,
} from '';
import type { AccountsetsAccountSetIdTransfersExportGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TransfersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdTransfersExportGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdTransfersExportGet(body);
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


## accountsetsAccountSetIdTransfersImportPost

> TransferImportResult accountsetsAccountSetIdTransfersImportPost(accountSetId, file)



### Example

```ts
import {
  Configuration,
  TransfersApi,
} from '';
import type { AccountsetsAccountSetIdTransfersImportPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TransfersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Blob (optional)
    file: BINARY_DATA_HERE,
  } satisfies AccountsetsAccountSetIdTransfersImportPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdTransfersImportPost(body);
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

[**TransferImportResult**](TransferImportResult.md)

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


## accountsetsAccountSetIdTransfersImportTemplateGet

> accountsetsAccountSetIdTransfersImportTemplateGet(accountSetId)



### Example

```ts
import {
  Configuration,
  TransfersApi,
} from '';
import type { AccountsetsAccountSetIdTransfersImportTemplateGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TransfersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdTransfersImportTemplateGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdTransfersImportTemplateGet(body);
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


## accountsetsAccountSetIdTransfersPdfGet

> accountsetsAccountSetIdTransfersPdfGet(accountSetId, startDate, endDate, showAll, date, summary, from, to, amount, voucher)



### Example

```ts
import {
  Configuration,
  TransfersApi,
} from '';
import type { AccountsetsAccountSetIdTransfersPdfGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TransfersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
    // boolean (optional)
    showAll: true,
    // string (optional)
    date: date_example,
    // string (optional)
    summary: summary_example,
    // string (optional)
    from: from_example,
    // string (optional)
    to: to_example,
    // string (optional)
    amount: amount_example,
    // string (optional)
    voucher: voucher_example,
  } satisfies AccountsetsAccountSetIdTransfersPdfGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdTransfersPdfGet(body);
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
| **showAll** | `boolean` |  | [Optional] [Defaults to `undefined`] |
| **date** | `string` |  | [Optional] [Defaults to `undefined`] |
| **summary** | `string` |  | [Optional] [Defaults to `undefined`] |
| **from** | `string` |  | [Optional] [Defaults to `undefined`] |
| **to** | `string` |  | [Optional] [Defaults to `undefined`] |
| **amount** | `string` |  | [Optional] [Defaults to `undefined`] |
| **voucher** | `string` |  | [Optional] [Defaults to `undefined`] |

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


## accountsetsAccountSetIdTransfersVouchersPost

> number accountsetsAccountSetIdTransfersVouchersPost(accountSetId, bulkTransferRequest)



### Example

```ts
import {
  Configuration,
  TransfersApi,
} from '';
import type { AccountsetsAccountSetIdTransfersVouchersPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TransfersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // BulkTransferRequest
    bulkTransferRequest: ...,
  } satisfies AccountsetsAccountSetIdTransfersVouchersPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdTransfersVouchersPost(body);
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
| **bulkTransferRequest** | [BulkTransferRequest](BulkTransferRequest.md) |  | |

### Return type

**number**

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

