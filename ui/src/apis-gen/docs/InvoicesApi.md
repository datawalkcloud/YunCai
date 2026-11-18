# InvoicesApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdInvoicesBulkAuthenticatePost**](InvoicesApi.md#accountsetsaccountsetidinvoicesbulkauthenticatepost) | **POST** /accountsets/{accountSetId}/invoices/bulk-authenticate |  |
| [**accountsetsAccountSetIdInvoicesBulkDeletePost**](InvoicesApi.md#accountsetsaccountsetidinvoicesbulkdeletepost) | **POST** /accountsets/{accountSetId}/invoices/bulk-delete |  |
| [**accountsetsAccountSetIdInvoicesBulkStatusPost**](InvoicesApi.md#accountsetsaccountsetidinvoicesbulkstatuspost) | **POST** /accountsets/{accountSetId}/invoices/bulk-status |  |
| [**accountsetsAccountSetIdInvoicesExportGet**](InvoicesApi.md#accountsetsaccountsetidinvoicesexportget) | **GET** /accountsets/{accountSetId}/invoices/export |  |
| [**accountsetsAccountSetIdInvoicesFundOverviewExportGet**](InvoicesApi.md#accountsetsaccountsetidinvoicesfundoverviewexportget) | **GET** /accountsets/{accountSetId}/invoices/fund-overview/export |  |
| [**accountsetsAccountSetIdInvoicesFundOverviewGet**](InvoicesApi.md#accountsetsaccountsetidinvoicesfundoverviewget) | **GET** /accountsets/{accountSetId}/invoices/fund-overview |  |
| [**accountsetsAccountSetIdInvoicesGet**](InvoicesApi.md#accountsetsaccountsetidinvoicesget) | **GET** /accountsets/{accountSetId}/invoices |  |
| [**accountsetsAccountSetIdInvoicesIdAuthenticatePost**](InvoicesApi.md#accountsetsaccountsetidinvoicesidauthenticatepost) | **POST** /accountsets/{accountSetId}/invoices/{id}/authenticate |  |
| [**accountsetsAccountSetIdInvoicesIdDelete**](InvoicesApi.md#accountsetsaccountsetidinvoicesiddelete) | **DELETE** /accountsets/{accountSetId}/invoices/{id} |  |
| [**accountsetsAccountSetIdInvoicesIdPut**](InvoicesApi.md#accountsetsaccountsetidinvoicesidput) | **PUT** /accountsets/{accountSetId}/invoices/{id} |  |
| [**accountsetsAccountSetIdInvoicesIdStatusPost**](InvoicesApi.md#accountsetsaccountsetidinvoicesidstatuspost) | **POST** /accountsets/{accountSetId}/invoices/{id}/status |  |
| [**accountsetsAccountSetIdInvoicesIdVoucherPost**](InvoicesApi.md#accountsetsaccountsetidinvoicesidvoucherpost) | **POST** /accountsets/{accountSetId}/invoices/{id}/voucher |  |
| [**accountsetsAccountSetIdInvoicesImportPost**](InvoicesApi.md#accountsetsaccountsetidinvoicesimportpost) | **POST** /accountsets/{accountSetId}/invoices/import |  |
| [**accountsetsAccountSetIdInvoicesImportTemplateGet**](InvoicesApi.md#accountsetsaccountsetidinvoicesimporttemplateget) | **GET** /accountsets/{accountSetId}/invoices/import-template |  |
| [**accountsetsAccountSetIdInvoicesPdfGet**](InvoicesApi.md#accountsetsaccountsetidinvoicespdfget) | **GET** /accountsets/{accountSetId}/invoices/pdf |  |
| [**accountsetsAccountSetIdInvoicesPost**](InvoicesApi.md#accountsetsaccountsetidinvoicespost) | **POST** /accountsets/{accountSetId}/invoices |  |
| [**accountsetsAccountSetIdInvoicesSummaryGet**](InvoicesApi.md#accountsetsaccountsetidinvoicessummaryget) | **GET** /accountsets/{accountSetId}/invoices/summary |  |
| [**accountsetsAccountSetIdInvoicesVouchersPost**](InvoicesApi.md#accountsetsaccountsetidinvoicesvoucherspost) | **POST** /accountsets/{accountSetId}/invoices/vouchers |  |



## accountsetsAccountSetIdInvoicesBulkAuthenticatePost

> BulkInvoiceResultDto accountsetsAccountSetIdInvoicesBulkAuthenticatePost(accountSetId, bulkAuthenticateRequest)



### Example

```ts
import {
  Configuration,
  InvoicesApi,
} from '';
import type { AccountsetsAccountSetIdInvoicesBulkAuthenticatePostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new InvoicesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // BulkAuthenticateRequest
    bulkAuthenticateRequest: ...,
  } satisfies AccountsetsAccountSetIdInvoicesBulkAuthenticatePostRequest;

  try {
    const data = await api.accountsetsAccountSetIdInvoicesBulkAuthenticatePost(body);
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
| **bulkAuthenticateRequest** | [BulkAuthenticateRequest](BulkAuthenticateRequest.md) |  | |

### Return type

[**BulkInvoiceResultDto**](BulkInvoiceResultDto.md)

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


## accountsetsAccountSetIdInvoicesBulkDeletePost

> BulkInvoiceResultDto accountsetsAccountSetIdInvoicesBulkDeletePost(accountSetId, bulkInvoiceIdsRequest)



### Example

```ts
import {
  Configuration,
  InvoicesApi,
} from '';
import type { AccountsetsAccountSetIdInvoicesBulkDeletePostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new InvoicesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // BulkInvoiceIdsRequest
    bulkInvoiceIdsRequest: ...,
  } satisfies AccountsetsAccountSetIdInvoicesBulkDeletePostRequest;

  try {
    const data = await api.accountsetsAccountSetIdInvoicesBulkDeletePost(body);
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
| **bulkInvoiceIdsRequest** | [BulkInvoiceIdsRequest](BulkInvoiceIdsRequest.md) |  | |

### Return type

[**BulkInvoiceResultDto**](BulkInvoiceResultDto.md)

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


## accountsetsAccountSetIdInvoicesBulkStatusPost

> BulkInvoiceResultDto accountsetsAccountSetIdInvoicesBulkStatusPost(accountSetId, bulkInvoiceStatusRequest)



### Example

```ts
import {
  Configuration,
  InvoicesApi,
} from '';
import type { AccountsetsAccountSetIdInvoicesBulkStatusPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new InvoicesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // BulkInvoiceStatusRequest
    bulkInvoiceStatusRequest: ...,
  } satisfies AccountsetsAccountSetIdInvoicesBulkStatusPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdInvoicesBulkStatusPost(body);
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
| **bulkInvoiceStatusRequest** | [BulkInvoiceStatusRequest](BulkInvoiceStatusRequest.md) |  | |

### Return type

[**BulkInvoiceResultDto**](BulkInvoiceResultDto.md)

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


## accountsetsAccountSetIdInvoicesExportGet

> accountsetsAccountSetIdInvoicesExportGet(accountSetId, direction, startDate, endDate, status, period)



### Example

```ts
import {
  Configuration,
  InvoicesApi,
} from '';
import type { AccountsetsAccountSetIdInvoicesExportGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new InvoicesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    direction: 56,
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
    // number (optional)
    status: 56,
    // string (optional)
    period: period_example,
  } satisfies AccountsetsAccountSetIdInvoicesExportGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdInvoicesExportGet(body);
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
| **direction** | `number` |  | [Optional] [Defaults to `undefined`] |
| **startDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **endDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **status** | `number` |  | [Optional] [Defaults to `undefined`] |
| **period** | `string` |  | [Optional] [Defaults to `undefined`] |

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


## accountsetsAccountSetIdInvoicesFundOverviewExportGet

> accountsetsAccountSetIdInvoicesFundOverviewExportGet(accountSetId, startDate, endDate, period)



### Example

```ts
import {
  Configuration,
  InvoicesApi,
} from '';
import type { AccountsetsAccountSetIdInvoicesFundOverviewExportGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new InvoicesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
    // string (optional)
    period: period_example,
  } satisfies AccountsetsAccountSetIdInvoicesFundOverviewExportGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdInvoicesFundOverviewExportGet(body);
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
| **period** | `string` |  | [Optional] [Defaults to `undefined`] |

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


## accountsetsAccountSetIdInvoicesFundOverviewGet

> Array&lt;InvoiceFundOverviewRowDto&gt; accountsetsAccountSetIdInvoicesFundOverviewGet(accountSetId, startDate, endDate)



### Example

```ts
import {
  Configuration,
  InvoicesApi,
} from '';
import type { AccountsetsAccountSetIdInvoicesFundOverviewGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new InvoicesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
  } satisfies AccountsetsAccountSetIdInvoicesFundOverviewGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdInvoicesFundOverviewGet(body);
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

[**Array&lt;InvoiceFundOverviewRowDto&gt;**](InvoiceFundOverviewRowDto.md)

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


## accountsetsAccountSetIdInvoicesGet

> Array&lt;InvoiceDto&gt; accountsetsAccountSetIdInvoicesGet(accountSetId, direction, startDate, endDate, status)



### Example

```ts
import {
  Configuration,
  InvoicesApi,
} from '';
import type { AccountsetsAccountSetIdInvoicesGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new InvoicesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    direction: 56,
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
    // number (optional)
    status: 56,
  } satisfies AccountsetsAccountSetIdInvoicesGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdInvoicesGet(body);
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
| **direction** | `number` |  | [Optional] [Defaults to `undefined`] |
| **startDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **endDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **status** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**Array&lt;InvoiceDto&gt;**](InvoiceDto.md)

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


## accountsetsAccountSetIdInvoicesIdAuthenticatePost

> InvoiceDto accountsetsAccountSetIdInvoicesIdAuthenticatePost(accountSetId, id, authenticateInvoiceRequest)



### Example

```ts
import {
  Configuration,
  InvoicesApi,
} from '';
import type { AccountsetsAccountSetIdInvoicesIdAuthenticatePostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new InvoicesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // AuthenticateInvoiceRequest
    authenticateInvoiceRequest: ...,
  } satisfies AccountsetsAccountSetIdInvoicesIdAuthenticatePostRequest;

  try {
    const data = await api.accountsetsAccountSetIdInvoicesIdAuthenticatePost(body);
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
| **authenticateInvoiceRequest** | [AuthenticateInvoiceRequest](AuthenticateInvoiceRequest.md) |  | |

### Return type

[**InvoiceDto**](InvoiceDto.md)

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


## accountsetsAccountSetIdInvoicesIdDelete

> accountsetsAccountSetIdInvoicesIdDelete(accountSetId, id)



### Example

```ts
import {
  Configuration,
  InvoicesApi,
} from '';
import type { AccountsetsAccountSetIdInvoicesIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new InvoicesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdInvoicesIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdInvoicesIdDelete(body);
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


## accountsetsAccountSetIdInvoicesIdPut

> InvoiceDto accountsetsAccountSetIdInvoicesIdPut(accountSetId, id, saveInvoiceRequest)



### Example

```ts
import {
  Configuration,
  InvoicesApi,
} from '';
import type { AccountsetsAccountSetIdInvoicesIdPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new InvoicesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveInvoiceRequest
    saveInvoiceRequest: ...,
  } satisfies AccountsetsAccountSetIdInvoicesIdPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdInvoicesIdPut(body);
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
| **saveInvoiceRequest** | [SaveInvoiceRequest](SaveInvoiceRequest.md) |  | |

### Return type

[**InvoiceDto**](InvoiceDto.md)

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


## accountsetsAccountSetIdInvoicesIdStatusPost

> InvoiceDto accountsetsAccountSetIdInvoicesIdStatusPost(accountSetId, id, changeInvoiceStatusRequest)



### Example

```ts
import {
  Configuration,
  InvoicesApi,
} from '';
import type { AccountsetsAccountSetIdInvoicesIdStatusPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new InvoicesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // ChangeInvoiceStatusRequest
    changeInvoiceStatusRequest: ...,
  } satisfies AccountsetsAccountSetIdInvoicesIdStatusPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdInvoicesIdStatusPost(body);
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
| **changeInvoiceStatusRequest** | [ChangeInvoiceStatusRequest](ChangeInvoiceStatusRequest.md) |  | |

### Return type

[**InvoiceDto**](InvoiceDto.md)

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


## accountsetsAccountSetIdInvoicesIdVoucherPost

> InvoiceVoucherResultDto accountsetsAccountSetIdInvoicesIdVoucherPost(accountSetId, id, generateInvoiceVoucherRequest)



### Example

```ts
import {
  Configuration,
  InvoicesApi,
} from '';
import type { AccountsetsAccountSetIdInvoicesIdVoucherPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new InvoicesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // GenerateInvoiceVoucherRequest
    generateInvoiceVoucherRequest: ...,
  } satisfies AccountsetsAccountSetIdInvoicesIdVoucherPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdInvoicesIdVoucherPost(body);
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
| **generateInvoiceVoucherRequest** | [GenerateInvoiceVoucherRequest](GenerateInvoiceVoucherRequest.md) |  | |

### Return type

[**InvoiceVoucherResultDto**](InvoiceVoucherResultDto.md)

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


## accountsetsAccountSetIdInvoicesImportPost

> InvoiceImportResult accountsetsAccountSetIdInvoicesImportPost(accountSetId, direction, file)



### Example

```ts
import {
  Configuration,
  InvoicesApi,
} from '';
import type { AccountsetsAccountSetIdInvoicesImportPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new InvoicesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    direction: 56,
    // Blob (optional)
    file: BINARY_DATA_HERE,
  } satisfies AccountsetsAccountSetIdInvoicesImportPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdInvoicesImportPost(body);
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
| **direction** | `number` |  | [Optional] [Defaults to `undefined`] |
| **file** | `Blob` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**InvoiceImportResult**](InvoiceImportResult.md)

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


## accountsetsAccountSetIdInvoicesImportTemplateGet

> accountsetsAccountSetIdInvoicesImportTemplateGet(accountSetId, direction)



### Example

```ts
import {
  Configuration,
  InvoicesApi,
} from '';
import type { AccountsetsAccountSetIdInvoicesImportTemplateGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new InvoicesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    direction: 56,
  } satisfies AccountsetsAccountSetIdInvoicesImportTemplateGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdInvoicesImportTemplateGet(body);
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
| **direction** | `number` |  | [Optional] [Defaults to `undefined`] |

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


## accountsetsAccountSetIdInvoicesPdfGet

> accountsetsAccountSetIdInvoicesPdfGet(accountSetId, direction, startDate, endDate, status, period)



### Example

```ts
import {
  Configuration,
  InvoicesApi,
} from '';
import type { AccountsetsAccountSetIdInvoicesPdfGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new InvoicesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    direction: 56,
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
    // number (optional)
    status: 56,
    // string (optional)
    period: period_example,
  } satisfies AccountsetsAccountSetIdInvoicesPdfGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdInvoicesPdfGet(body);
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
| **direction** | `number` |  | [Optional] [Defaults to `undefined`] |
| **startDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **endDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **status** | `number` |  | [Optional] [Defaults to `undefined`] |
| **period** | `string` |  | [Optional] [Defaults to `undefined`] |

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


## accountsetsAccountSetIdInvoicesPost

> InvoiceDto accountsetsAccountSetIdInvoicesPost(accountSetId, saveInvoiceRequest, direction)



### Example

```ts
import {
  Configuration,
  InvoicesApi,
} from '';
import type { AccountsetsAccountSetIdInvoicesPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new InvoicesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveInvoiceRequest
    saveInvoiceRequest: ...,
    // number (optional)
    direction: 56,
  } satisfies AccountsetsAccountSetIdInvoicesPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdInvoicesPost(body);
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
| **saveInvoiceRequest** | [SaveInvoiceRequest](SaveInvoiceRequest.md) |  | |
| **direction** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**InvoiceDto**](InvoiceDto.md)

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


## accountsetsAccountSetIdInvoicesSummaryGet

> InvoiceSummaryDto accountsetsAccountSetIdInvoicesSummaryGet(accountSetId, direction, startDate, endDate, status)



### Example

```ts
import {
  Configuration,
  InvoicesApi,
} from '';
import type { AccountsetsAccountSetIdInvoicesSummaryGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new InvoicesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    direction: 56,
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
    // number (optional)
    status: 56,
  } satisfies AccountsetsAccountSetIdInvoicesSummaryGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdInvoicesSummaryGet(body);
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
| **direction** | `number` |  | [Optional] [Defaults to `undefined`] |
| **startDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **endDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **status** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**InvoiceSummaryDto**](InvoiceSummaryDto.md)

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


## accountsetsAccountSetIdInvoicesVouchersPost

> InvoiceVoucherBatchResultDto accountsetsAccountSetIdInvoicesVouchersPost(accountSetId, generateInvoiceVouchersRequest)



### Example

```ts
import {
  Configuration,
  InvoicesApi,
} from '';
import type { AccountsetsAccountSetIdInvoicesVouchersPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new InvoicesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // GenerateInvoiceVouchersRequest
    generateInvoiceVouchersRequest: ...,
  } satisfies AccountsetsAccountSetIdInvoicesVouchersPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdInvoicesVouchersPost(body);
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
| **generateInvoiceVouchersRequest** | [GenerateInvoiceVouchersRequest](GenerateInvoiceVouchersRequest.md) |  | |

### Return type

[**InvoiceVoucherBatchResultDto**](InvoiceVoucherBatchResultDto.md)

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

