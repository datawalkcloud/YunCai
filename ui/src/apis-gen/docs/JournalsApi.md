# JournalsApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdJournalsAccountsImportPost**](JournalsApi.md#accountsetsaccountsetidjournalsaccountsimportpost) | **POST** /accountsets/{accountSetId}/Journals/accounts/import |  |
| [**accountsetsAccountSetIdJournalsAccountsImportTemplateGet**](JournalsApi.md#accountsetsaccountsetidjournalsaccountsimporttemplateget) | **GET** /accountsets/{accountSetId}/Journals/accounts/import-template |  |
| [**accountsetsAccountSetIdJournalsExportAllGet**](JournalsApi.md#accountsetsaccountsetidjournalsexportallget) | **GET** /accountsets/{accountSetId}/Journals/export/all |  |
| [**accountsetsAccountSetIdJournalsGet**](JournalsApi.md#accountsetsaccountsetidjournalsget) | **GET** /accountsets/{accountSetId}/Journals |  |
| [**accountsetsAccountSetIdJournalsIdDelete**](JournalsApi.md#accountsetsaccountsetidjournalsiddelete) | **DELETE** /accountsets/{accountSetId}/Journals/{id} |  |
| [**accountsetsAccountSetIdJournalsIdEnabledPatch**](JournalsApi.md#accountsetsaccountsetidjournalsidenabledpatch) | **PATCH** /accountsets/{accountSetId}/Journals/{id}/enabled |  |
| [**accountsetsAccountSetIdJournalsIdEntriesBulkPost**](JournalsApi.md#accountsetsaccountsetidjournalsidentriesbulkpost) | **POST** /accountsets/{accountSetId}/Journals/{id}/entries/bulk |  |
| [**accountsetsAccountSetIdJournalsIdEntriesEntryIdDelete**](JournalsApi.md#accountsetsaccountsetidjournalsidentriesentryiddelete) | **DELETE** /accountsets/{accountSetId}/Journals/{id}/entries/{entryId} |  |
| [**accountsetsAccountSetIdJournalsIdEntriesEntryIdPut**](JournalsApi.md#accountsetsaccountsetidjournalsidentriesentryidput) | **PUT** /accountsets/{accountSetId}/Journals/{id}/entries/{entryId} |  |
| [**accountsetsAccountSetIdJournalsIdEntriesEntryIdVoucherPost**](JournalsApi.md#accountsetsaccountsetidjournalsidentriesentryidvoucherpost) | **POST** /accountsets/{accountSetId}/Journals/{id}/entries/{entryId}/voucher |  |
| [**accountsetsAccountSetIdJournalsIdEntriesPost**](JournalsApi.md#accountsetsaccountsetidjournalsidentriespost) | **POST** /accountsets/{accountSetId}/Journals/{id}/entries |  |
| [**accountsetsAccountSetIdJournalsIdEntriesReceiptPdfGet**](JournalsApi.md#accountsetsaccountsetidjournalsidentriesreceiptpdfget) | **GET** /accountsets/{accountSetId}/Journals/{id}/entries/receipt/pdf |  |
| [**accountsetsAccountSetIdJournalsIdEntriesVouchersPost**](JournalsApi.md#accountsetsaccountsetidjournalsidentriesvoucherspost) | **POST** /accountsets/{accountSetId}/Journals/{id}/entries/vouchers |  |
| [**accountsetsAccountSetIdJournalsIdExportGet**](JournalsApi.md#accountsetsaccountsetidjournalsidexportget) | **GET** /accountsets/{accountSetId}/Journals/{id}/export |  |
| [**accountsetsAccountSetIdJournalsIdGet**](JournalsApi.md#accountsetsaccountsetidjournalsidget) | **GET** /accountsets/{accountSetId}/Journals/{id} |  |
| [**accountsetsAccountSetIdJournalsIdImportPost**](JournalsApi.md#accountsetsaccountsetidjournalsidimportpost) | **POST** /accountsets/{accountSetId}/Journals/{id}/import |  |
| [**accountsetsAccountSetIdJournalsIdPdfGet**](JournalsApi.md#accountsetsaccountsetidjournalsidpdfget) | **GET** /accountsets/{accountSetId}/Journals/{id}/pdf |  |
| [**accountsetsAccountSetIdJournalsIdPut**](JournalsApi.md#accountsetsaccountsetidjournalsidput) | **PUT** /accountsets/{accountSetId}/Journals/{id} |  |
| [**accountsetsAccountSetIdJournalsImportTemplateGet**](JournalsApi.md#accountsetsaccountsetidjournalsimporttemplateget) | **GET** /accountsets/{accountSetId}/Journals/import/template |  |
| [**accountsetsAccountSetIdJournalsPost**](JournalsApi.md#accountsetsaccountsetidjournalspost) | **POST** /accountsets/{accountSetId}/Journals |  |



## accountsetsAccountSetIdJournalsAccountsImportPost

> JournalAccountImportResultDto accountsetsAccountSetIdJournalsAccountsImportPost(accountSetId, file)



### Example

```ts
import {
  Configuration,
  JournalsApi,
} from '';
import type { AccountsetsAccountSetIdJournalsAccountsImportPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new JournalsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Blob (optional)
    file: BINARY_DATA_HERE,
  } satisfies AccountsetsAccountSetIdJournalsAccountsImportPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdJournalsAccountsImportPost(body);
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

[**JournalAccountImportResultDto**](JournalAccountImportResultDto.md)

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


## accountsetsAccountSetIdJournalsAccountsImportTemplateGet

> accountsetsAccountSetIdJournalsAccountsImportTemplateGet(accountSetId)



### Example

```ts
import {
  Configuration,
  JournalsApi,
} from '';
import type { AccountsetsAccountSetIdJournalsAccountsImportTemplateGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new JournalsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdJournalsAccountsImportTemplateGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdJournalsAccountsImportTemplateGet(body);
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


## accountsetsAccountSetIdJournalsExportAllGet

> accountsetsAccountSetIdJournalsExportAllGet(accountSetId)



### Example

```ts
import {
  Configuration,
  JournalsApi,
} from '';
import type { AccountsetsAccountSetIdJournalsExportAllGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new JournalsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdJournalsExportAllGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdJournalsExportAllGet(body);
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


## accountsetsAccountSetIdJournalsGet

> Array&lt;JournalListItemDto&gt; accountsetsAccountSetIdJournalsGet(accountSetId)



### Example

```ts
import {
  Configuration,
  JournalsApi,
} from '';
import type { AccountsetsAccountSetIdJournalsGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new JournalsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdJournalsGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdJournalsGet(body);
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

[**Array&lt;JournalListItemDto&gt;**](JournalListItemDto.md)

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


## accountsetsAccountSetIdJournalsIdDelete

> accountsetsAccountSetIdJournalsIdDelete(accountSetId, id)



### Example

```ts
import {
  Configuration,
  JournalsApi,
} from '';
import type { AccountsetsAccountSetIdJournalsIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new JournalsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdJournalsIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdJournalsIdDelete(body);
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


## accountsetsAccountSetIdJournalsIdEnabledPatch

> JournalDetailDto accountsetsAccountSetIdJournalsIdEnabledPatch(accountSetId, id, setJournalEnabledRequest)



### Example

```ts
import {
  Configuration,
  JournalsApi,
} from '';
import type { AccountsetsAccountSetIdJournalsIdEnabledPatchRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new JournalsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SetJournalEnabledRequest
    setJournalEnabledRequest: ...,
  } satisfies AccountsetsAccountSetIdJournalsIdEnabledPatchRequest;

  try {
    const data = await api.accountsetsAccountSetIdJournalsIdEnabledPatch(body);
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
| **setJournalEnabledRequest** | [SetJournalEnabledRequest](SetJournalEnabledRequest.md) |  | |

### Return type

[**JournalDetailDto**](JournalDetailDto.md)

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


## accountsetsAccountSetIdJournalsIdEntriesBulkPost

> number accountsetsAccountSetIdJournalsIdEntriesBulkPost(accountSetId, id, bulkJournalEntryRequest)



### Example

```ts
import {
  Configuration,
  JournalsApi,
} from '';
import type { AccountsetsAccountSetIdJournalsIdEntriesBulkPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new JournalsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // BulkJournalEntryRequest
    bulkJournalEntryRequest: ...,
  } satisfies AccountsetsAccountSetIdJournalsIdEntriesBulkPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdJournalsIdEntriesBulkPost(body);
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
| **bulkJournalEntryRequest** | [BulkJournalEntryRequest](BulkJournalEntryRequest.md) |  | |

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


## accountsetsAccountSetIdJournalsIdEntriesEntryIdDelete

> JournalDetailDto accountsetsAccountSetIdJournalsIdEntriesEntryIdDelete(accountSetId, id, entryId)



### Example

```ts
import {
  Configuration,
  JournalsApi,
} from '';
import type { AccountsetsAccountSetIdJournalsIdEntriesEntryIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new JournalsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    entryId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdJournalsIdEntriesEntryIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdJournalsIdEntriesEntryIdDelete(body);
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
| **entryId** | `string` |  | [Defaults to `undefined`] |

### Return type

[**JournalDetailDto**](JournalDetailDto.md)

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


## accountsetsAccountSetIdJournalsIdEntriesEntryIdPut

> JournalDetailDto accountsetsAccountSetIdJournalsIdEntriesEntryIdPut(accountSetId, id, entryId, journalEntryRequest)



### Example

```ts
import {
  Configuration,
  JournalsApi,
} from '';
import type { AccountsetsAccountSetIdJournalsIdEntriesEntryIdPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new JournalsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    entryId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // JournalEntryRequest
    journalEntryRequest: ...,
  } satisfies AccountsetsAccountSetIdJournalsIdEntriesEntryIdPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdJournalsIdEntriesEntryIdPut(body);
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
| **entryId** | `string` |  | [Defaults to `undefined`] |
| **journalEntryRequest** | [JournalEntryRequest](JournalEntryRequest.md) |  | |

### Return type

[**JournalDetailDto**](JournalDetailDto.md)

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


## accountsetsAccountSetIdJournalsIdEntriesEntryIdVoucherPost

> GenerateVoucherResultDto accountsetsAccountSetIdJournalsIdEntriesEntryIdVoucherPost(accountSetId, id, entryId, generateVoucherRequest)



### Example

```ts
import {
  Configuration,
  JournalsApi,
} from '';
import type { AccountsetsAccountSetIdJournalsIdEntriesEntryIdVoucherPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new JournalsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    entryId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // GenerateVoucherRequest
    generateVoucherRequest: ...,
  } satisfies AccountsetsAccountSetIdJournalsIdEntriesEntryIdVoucherPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdJournalsIdEntriesEntryIdVoucherPost(body);
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
| **entryId** | `string` |  | [Defaults to `undefined`] |
| **generateVoucherRequest** | [GenerateVoucherRequest](GenerateVoucherRequest.md) |  | |

### Return type

[**GenerateVoucherResultDto**](GenerateVoucherResultDto.md)

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


## accountsetsAccountSetIdJournalsIdEntriesPost

> JournalDetailDto accountsetsAccountSetIdJournalsIdEntriesPost(accountSetId, id, journalEntryRequest)



### Example

```ts
import {
  Configuration,
  JournalsApi,
} from '';
import type { AccountsetsAccountSetIdJournalsIdEntriesPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new JournalsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // JournalEntryRequest
    journalEntryRequest: ...,
  } satisfies AccountsetsAccountSetIdJournalsIdEntriesPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdJournalsIdEntriesPost(body);
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
| **journalEntryRequest** | [JournalEntryRequest](JournalEntryRequest.md) |  | |

### Return type

[**JournalDetailDto**](JournalDetailDto.md)

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


## accountsetsAccountSetIdJournalsIdEntriesReceiptPdfGet

> accountsetsAccountSetIdJournalsIdEntriesReceiptPdfGet(accountSetId, id, entryIds)



### Example

```ts
import {
  Configuration,
  JournalsApi,
} from '';
import type { AccountsetsAccountSetIdJournalsIdEntriesReceiptPdfGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new JournalsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string (optional)
    entryIds: entryIds_example,
  } satisfies AccountsetsAccountSetIdJournalsIdEntriesReceiptPdfGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdJournalsIdEntriesReceiptPdfGet(body);
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
| **entryIds** | `string` |  | [Optional] [Defaults to `undefined`] |

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


## accountsetsAccountSetIdJournalsIdEntriesVouchersPost

> GenerateVouchersBatchResultDto accountsetsAccountSetIdJournalsIdEntriesVouchersPost(accountSetId, id, generateVouchersBatchRequest)



### Example

```ts
import {
  Configuration,
  JournalsApi,
} from '';
import type { AccountsetsAccountSetIdJournalsIdEntriesVouchersPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new JournalsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // GenerateVouchersBatchRequest
    generateVouchersBatchRequest: ...,
  } satisfies AccountsetsAccountSetIdJournalsIdEntriesVouchersPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdJournalsIdEntriesVouchersPost(body);
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
| **generateVouchersBatchRequest** | [GenerateVouchersBatchRequest](GenerateVouchersBatchRequest.md) |  | |

### Return type

[**GenerateVouchersBatchResultDto**](GenerateVouchersBatchResultDto.md)

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


## accountsetsAccountSetIdJournalsIdExportGet

> accountsetsAccountSetIdJournalsIdExportGet(accountSetId, id)



### Example

```ts
import {
  Configuration,
  JournalsApi,
} from '';
import type { AccountsetsAccountSetIdJournalsIdExportGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new JournalsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdJournalsIdExportGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdJournalsIdExportGet(body);
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


## accountsetsAccountSetIdJournalsIdGet

> JournalDetailDto accountsetsAccountSetIdJournalsIdGet(accountSetId, id)



### Example

```ts
import {
  Configuration,
  JournalsApi,
} from '';
import type { AccountsetsAccountSetIdJournalsIdGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new JournalsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdJournalsIdGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdJournalsIdGet(body);
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

[**JournalDetailDto**](JournalDetailDto.md)

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


## accountsetsAccountSetIdJournalsIdImportPost

> JournalImportResult accountsetsAccountSetIdJournalsIdImportPost(accountSetId, id, file)



### Example

```ts
import {
  Configuration,
  JournalsApi,
} from '';
import type { AccountsetsAccountSetIdJournalsIdImportPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new JournalsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Blob (optional)
    file: BINARY_DATA_HERE,
  } satisfies AccountsetsAccountSetIdJournalsIdImportPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdJournalsIdImportPost(body);
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
| **file** | `Blob` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**JournalImportResult**](JournalImportResult.md)

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


## accountsetsAccountSetIdJournalsIdPdfGet

> accountsetsAccountSetIdJournalsIdPdfGet(accountSetId, id, startDate, endDate, columns, seqPrefix, date, summary, category, counterparty, income, expense, balance, voucher, seq, advVoucherDateFrom, advVoucherDateTo, advVoucherWord, advVoucherNoFrom, advVoucherNoTo, advCategory, advSettlementMethod, advCounterparty, advBillNo, advProject, advDepartment, advSummary, advRemark, advSource, advAmountFrom, advAmountTo, advHasVoucher)



### Example

```ts
import {
  Configuration,
  JournalsApi,
} from '';
import type { AccountsetsAccountSetIdJournalsIdPdfGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new JournalsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
    // string (optional)
    columns: columns_example,
    // string (optional)
    seqPrefix: seqPrefix_example,
    // string (optional)
    date: date_example,
    // string (optional)
    summary: summary_example,
    // string (optional)
    category: category_example,
    // string (optional)
    counterparty: counterparty_example,
    // string (optional)
    income: income_example,
    // string (optional)
    expense: expense_example,
    // string (optional)
    balance: balance_example,
    // string (optional)
    voucher: voucher_example,
    // string (optional)
    seq: seq_example,
    // Date (optional)
    advVoucherDateFrom: 2013-10-20,
    // Date (optional)
    advVoucherDateTo: 2013-10-20,
    // string (optional)
    advVoucherWord: advVoucherWord_example,
    // number (optional)
    advVoucherNoFrom: 56,
    // number (optional)
    advVoucherNoTo: 56,
    // string (optional)
    advCategory: advCategory_example,
    // string (optional)
    advSettlementMethod: advSettlementMethod_example,
    // string (optional)
    advCounterparty: advCounterparty_example,
    // string (optional)
    advBillNo: advBillNo_example,
    // string (optional)
    advProject: advProject_example,
    // string (optional)
    advDepartment: advDepartment_example,
    // string (optional)
    advSummary: advSummary_example,
    // string (optional)
    advRemark: advRemark_example,
    // string (optional)
    advSource: advSource_example,
    // number (optional)
    advAmountFrom: 1.2,
    // number (optional)
    advAmountTo: 1.2,
    // string (optional)
    advHasVoucher: advHasVoucher_example,
  } satisfies AccountsetsAccountSetIdJournalsIdPdfGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdJournalsIdPdfGet(body);
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
| **startDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **endDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **columns** | `string` |  | [Optional] [Defaults to `undefined`] |
| **seqPrefix** | `string` |  | [Optional] [Defaults to `undefined`] |
| **date** | `string` |  | [Optional] [Defaults to `undefined`] |
| **summary** | `string` |  | [Optional] [Defaults to `undefined`] |
| **category** | `string` |  | [Optional] [Defaults to `undefined`] |
| **counterparty** | `string` |  | [Optional] [Defaults to `undefined`] |
| **income** | `string` |  | [Optional] [Defaults to `undefined`] |
| **expense** | `string` |  | [Optional] [Defaults to `undefined`] |
| **balance** | `string` |  | [Optional] [Defaults to `undefined`] |
| **voucher** | `string` |  | [Optional] [Defaults to `undefined`] |
| **seq** | `string` |  | [Optional] [Defaults to `undefined`] |
| **advVoucherDateFrom** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **advVoucherDateTo** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **advVoucherWord** | `string` |  | [Optional] [Defaults to `undefined`] |
| **advVoucherNoFrom** | `number` |  | [Optional] [Defaults to `undefined`] |
| **advVoucherNoTo** | `number` |  | [Optional] [Defaults to `undefined`] |
| **advCategory** | `string` |  | [Optional] [Defaults to `undefined`] |
| **advSettlementMethod** | `string` |  | [Optional] [Defaults to `undefined`] |
| **advCounterparty** | `string` |  | [Optional] [Defaults to `undefined`] |
| **advBillNo** | `string` |  | [Optional] [Defaults to `undefined`] |
| **advProject** | `string` |  | [Optional] [Defaults to `undefined`] |
| **advDepartment** | `string` |  | [Optional] [Defaults to `undefined`] |
| **advSummary** | `string` |  | [Optional] [Defaults to `undefined`] |
| **advRemark** | `string` |  | [Optional] [Defaults to `undefined`] |
| **advSource** | `string` |  | [Optional] [Defaults to `undefined`] |
| **advAmountFrom** | `number` |  | [Optional] [Defaults to `undefined`] |
| **advAmountTo** | `number` |  | [Optional] [Defaults to `undefined`] |
| **advHasVoucher** | `string` |  | [Optional] [Defaults to `undefined`] |

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


## accountsetsAccountSetIdJournalsIdPut

> JournalDetailDto accountsetsAccountSetIdJournalsIdPut(accountSetId, id, updateJournalRequest)



### Example

```ts
import {
  Configuration,
  JournalsApi,
} from '';
import type { AccountsetsAccountSetIdJournalsIdPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new JournalsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // UpdateJournalRequest
    updateJournalRequest: ...,
  } satisfies AccountsetsAccountSetIdJournalsIdPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdJournalsIdPut(body);
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
| **updateJournalRequest** | [UpdateJournalRequest](UpdateJournalRequest.md) |  | |

### Return type

[**JournalDetailDto**](JournalDetailDto.md)

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


## accountsetsAccountSetIdJournalsImportTemplateGet

> accountsetsAccountSetIdJournalsImportTemplateGet(accountSetId)



### Example

```ts
import {
  Configuration,
  JournalsApi,
} from '';
import type { AccountsetsAccountSetIdJournalsImportTemplateGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new JournalsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdJournalsImportTemplateGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdJournalsImportTemplateGet(body);
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


## accountsetsAccountSetIdJournalsPost

> JournalDetailDto accountsetsAccountSetIdJournalsPost(accountSetId, createJournalRequest)



### Example

```ts
import {
  Configuration,
  JournalsApi,
} from '';
import type { AccountsetsAccountSetIdJournalsPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new JournalsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // CreateJournalRequest
    createJournalRequest: ...,
  } satisfies AccountsetsAccountSetIdJournalsPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdJournalsPost(body);
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
| **createJournalRequest** | [CreateJournalRequest](CreateJournalRequest.md) |  | |

### Return type

[**JournalDetailDto**](JournalDetailDto.md)

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

