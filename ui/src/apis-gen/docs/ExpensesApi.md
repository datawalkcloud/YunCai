# ExpensesApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdExpensebillsBulkDeletePost**](ExpensesApi.md#accountsetsaccountsetidexpensebillsbulkdeletepost) | **POST** /accountsets/{accountSetId}/expensebills/bulk-delete |  |
| [**accountsetsAccountSetIdExpensebillsExportGet**](ExpensesApi.md#accountsetsaccountsetidexpensebillsexportget) | **GET** /accountsets/{accountSetId}/expensebills/export |  |
| [**accountsetsAccountSetIdExpensebillsGet**](ExpensesApi.md#accountsetsaccountsetidexpensebillsget) | **GET** /accountsets/{accountSetId}/expensebills |  |
| [**accountsetsAccountSetIdExpensebillsIdDelete**](ExpensesApi.md#accountsetsaccountsetidexpensebillsiddelete) | **DELETE** /accountsets/{accountSetId}/expensebills/{id} |  |
| [**accountsetsAccountSetIdExpensebillsIdPut**](ExpensesApi.md#accountsetsaccountsetidexpensebillsidput) | **PUT** /accountsets/{accountSetId}/expensebills/{id} |  |
| [**accountsetsAccountSetIdExpensebillsIdVoucherPost**](ExpensesApi.md#accountsetsaccountsetidexpensebillsidvoucherpost) | **POST** /accountsets/{accountSetId}/expensebills/{id}/voucher |  |
| [**accountsetsAccountSetIdExpensebillsImportPost**](ExpensesApi.md#accountsetsaccountsetidexpensebillsimportpost) | **POST** /accountsets/{accountSetId}/expensebills/import |  |
| [**accountsetsAccountSetIdExpensebillsImportTemplateGet**](ExpensesApi.md#accountsetsaccountsetidexpensebillsimporttemplateget) | **GET** /accountsets/{accountSetId}/expensebills/import-template |  |
| [**accountsetsAccountSetIdExpensebillsPost**](ExpensesApi.md#accountsetsaccountsetidexpensebillspost) | **POST** /accountsets/{accountSetId}/expensebills |  |
| [**accountsetsAccountSetIdExpensebillsSummaryGet**](ExpensesApi.md#accountsetsaccountsetidexpensebillssummaryget) | **GET** /accountsets/{accountSetId}/expensebills/summary |  |
| [**accountsetsAccountSetIdExpensebillsVouchersPost**](ExpensesApi.md#accountsetsaccountsetidexpensebillsvoucherspost) | **POST** /accountsets/{accountSetId}/expensebills/vouchers |  |



## accountsetsAccountSetIdExpensebillsBulkDeletePost

> ExpenseBulkResultDto accountsetsAccountSetIdExpensebillsBulkDeletePost(accountSetId, expenseBulkIdsRequest)



### Example

```ts
import {
  Configuration,
  ExpensesApi,
} from '';
import type { AccountsetsAccountSetIdExpensebillsBulkDeletePostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ExpensesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // ExpenseBulkIdsRequest
    expenseBulkIdsRequest: ...,
  } satisfies AccountsetsAccountSetIdExpensebillsBulkDeletePostRequest;

  try {
    const data = await api.accountsetsAccountSetIdExpensebillsBulkDeletePost(body);
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
| **expenseBulkIdsRequest** | [ExpenseBulkIdsRequest](ExpenseBulkIdsRequest.md) |  | |

### Return type

[**ExpenseBulkResultDto**](ExpenseBulkResultDto.md)

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


## accountsetsAccountSetIdExpensebillsExportGet

> accountsetsAccountSetIdExpensebillsExportGet(accountSetId)



### Example

```ts
import {
  Configuration,
  ExpensesApi,
} from '';
import type { AccountsetsAccountSetIdExpensebillsExportGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ExpensesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdExpensebillsExportGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdExpensebillsExportGet(body);
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


## accountsetsAccountSetIdExpensebillsGet

> Array&lt;ExpenseBillDto&gt; accountsetsAccountSetIdExpensebillsGet(accountSetId)



### Example

```ts
import {
  Configuration,
  ExpensesApi,
} from '';
import type { AccountsetsAccountSetIdExpensebillsGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ExpensesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdExpensebillsGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdExpensebillsGet(body);
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

[**Array&lt;ExpenseBillDto&gt;**](ExpenseBillDto.md)

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


## accountsetsAccountSetIdExpensebillsIdDelete

> accountsetsAccountSetIdExpensebillsIdDelete(accountSetId, id)



### Example

```ts
import {
  Configuration,
  ExpensesApi,
} from '';
import type { AccountsetsAccountSetIdExpensebillsIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ExpensesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdExpensebillsIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdExpensebillsIdDelete(body);
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


## accountsetsAccountSetIdExpensebillsIdPut

> ExpenseBillDto accountsetsAccountSetIdExpensebillsIdPut(accountSetId, id, saveExpenseBillRequest)



### Example

```ts
import {
  Configuration,
  ExpensesApi,
} from '';
import type { AccountsetsAccountSetIdExpensebillsIdPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ExpensesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveExpenseBillRequest
    saveExpenseBillRequest: ...,
  } satisfies AccountsetsAccountSetIdExpensebillsIdPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdExpensebillsIdPut(body);
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
| **saveExpenseBillRequest** | [SaveExpenseBillRequest](SaveExpenseBillRequest.md) |  | |

### Return type

[**ExpenseBillDto**](ExpenseBillDto.md)

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


## accountsetsAccountSetIdExpensebillsIdVoucherPost

> ExpenseVoucherResultDto accountsetsAccountSetIdExpensebillsIdVoucherPost(accountSetId, id, generateExpenseVoucherRequest)



### Example

```ts
import {
  Configuration,
  ExpensesApi,
} from '';
import type { AccountsetsAccountSetIdExpensebillsIdVoucherPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ExpensesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // GenerateExpenseVoucherRequest
    generateExpenseVoucherRequest: ...,
  } satisfies AccountsetsAccountSetIdExpensebillsIdVoucherPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdExpensebillsIdVoucherPost(body);
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
| **generateExpenseVoucherRequest** | [GenerateExpenseVoucherRequest](GenerateExpenseVoucherRequest.md) |  | |

### Return type

[**ExpenseVoucherResultDto**](ExpenseVoucherResultDto.md)

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


## accountsetsAccountSetIdExpensebillsImportPost

> ExpenseImportResultDto accountsetsAccountSetIdExpensebillsImportPost(accountSetId, file)



### Example

```ts
import {
  Configuration,
  ExpensesApi,
} from '';
import type { AccountsetsAccountSetIdExpensebillsImportPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ExpensesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Blob (optional)
    file: BINARY_DATA_HERE,
  } satisfies AccountsetsAccountSetIdExpensebillsImportPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdExpensebillsImportPost(body);
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

[**ExpenseImportResultDto**](ExpenseImportResultDto.md)

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


## accountsetsAccountSetIdExpensebillsImportTemplateGet

> accountsetsAccountSetIdExpensebillsImportTemplateGet(accountSetId)



### Example

```ts
import {
  Configuration,
  ExpensesApi,
} from '';
import type { AccountsetsAccountSetIdExpensebillsImportTemplateGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ExpensesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdExpensebillsImportTemplateGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdExpensebillsImportTemplateGet(body);
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


## accountsetsAccountSetIdExpensebillsPost

> ExpenseBillDto accountsetsAccountSetIdExpensebillsPost(accountSetId, saveExpenseBillRequest)



### Example

```ts
import {
  Configuration,
  ExpensesApi,
} from '';
import type { AccountsetsAccountSetIdExpensebillsPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ExpensesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveExpenseBillRequest
    saveExpenseBillRequest: ...,
  } satisfies AccountsetsAccountSetIdExpensebillsPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdExpensebillsPost(body);
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
| **saveExpenseBillRequest** | [SaveExpenseBillRequest](SaveExpenseBillRequest.md) |  | |

### Return type

[**ExpenseBillDto**](ExpenseBillDto.md)

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


## accountsetsAccountSetIdExpensebillsSummaryGet

> ExpenseBillSummaryDto accountsetsAccountSetIdExpensebillsSummaryGet(accountSetId)



### Example

```ts
import {
  Configuration,
  ExpensesApi,
} from '';
import type { AccountsetsAccountSetIdExpensebillsSummaryGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ExpensesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdExpensebillsSummaryGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdExpensebillsSummaryGet(body);
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

[**ExpenseBillSummaryDto**](ExpenseBillSummaryDto.md)

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


## accountsetsAccountSetIdExpensebillsVouchersPost

> ExpenseVoucherBatchResultDto accountsetsAccountSetIdExpensebillsVouchersPost(accountSetId, generateExpenseVouchersRequest)



### Example

```ts
import {
  Configuration,
  ExpensesApi,
} from '';
import type { AccountsetsAccountSetIdExpensebillsVouchersPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ExpensesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // GenerateExpenseVouchersRequest
    generateExpenseVouchersRequest: ...,
  } satisfies AccountsetsAccountSetIdExpensebillsVouchersPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdExpensebillsVouchersPost(body);
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
| **generateExpenseVouchersRequest** | [GenerateExpenseVouchersRequest](GenerateExpenseVouchersRequest.md) |  | |

### Return type

[**ExpenseVoucherBatchResultDto**](ExpenseVoucherBatchResultDto.md)

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

