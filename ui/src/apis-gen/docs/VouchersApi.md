# VouchersApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdVouchersExportGet**](VouchersApi.md#accountsetsaccountsetidvouchersexportget) | **GET** /accountsets/{accountSetId}/Vouchers/export |  |
| [**accountsetsAccountSetIdVouchersExportPost**](VouchersApi.md#accountsetsaccountsetidvouchersexportpost) | **POST** /accountsets/{accountSetId}/Vouchers/export |  |
| [**accountsetsAccountSetIdVouchersGet**](VouchersApi.md#accountsetsaccountsetidvouchersget) | **GET** /accountsets/{accountSetId}/Vouchers |  |
| [**accountsetsAccountSetIdVouchersIdAttachmentsAttachmentIdDelete**](VouchersApi.md#accountsetsaccountsetidvouchersidattachmentsattachmentiddelete) | **DELETE** /accountsets/{accountSetId}/Vouchers/{id}/attachments/{attachmentId} |  |
| [**accountsetsAccountSetIdVouchersIdAttachmentsAttachmentIdFileGet**](VouchersApi.md#accountsetsaccountsetidvouchersidattachmentsattachmentidfileget) | **GET** /accountsets/{accountSetId}/Vouchers/{id}/attachments/{attachmentId}/file |  |
| [**accountsetsAccountSetIdVouchersIdAttachmentsGet**](VouchersApi.md#accountsetsaccountsetidvouchersidattachmentsget) | **GET** /accountsets/{accountSetId}/Vouchers/{id}/attachments |  |
| [**accountsetsAccountSetIdVouchersIdAttachmentsPost**](VouchersApi.md#accountsetsaccountsetidvouchersidattachmentspost) | **POST** /accountsets/{accountSetId}/Vouchers/{id}/attachments |  |
| [**accountsetsAccountSetIdVouchersIdCopyPost**](VouchersApi.md#accountsetsaccountsetidvouchersidcopypost) | **POST** /accountsets/{accountSetId}/Vouchers/{id}/copy |  |
| [**accountsetsAccountSetIdVouchersIdDelete**](VouchersApi.md#accountsetsaccountsetidvouchersiddelete) | **DELETE** /accountsets/{accountSetId}/Vouchers/{id} |  |
| [**accountsetsAccountSetIdVouchersIdGet**](VouchersApi.md#accountsetsaccountsetidvouchersidget) | **GET** /accountsets/{accountSetId}/Vouchers/{id} |  |
| [**accountsetsAccountSetIdVouchersIdPdfGet**](VouchersApi.md#accountsetsaccountsetidvouchersidpdfget) | **GET** /accountsets/{accountSetId}/Vouchers/{id}/pdf |  |
| [**accountsetsAccountSetIdVouchersIdPostPost**](VouchersApi.md#accountsetsaccountsetidvouchersidpostpost) | **POST** /accountsets/{accountSetId}/Vouchers/{id}/post |  |
| [**accountsetsAccountSetIdVouchersIdPurgeDelete**](VouchersApi.md#accountsetsaccountsetidvouchersidpurgedelete) | **DELETE** /accountsets/{accountSetId}/Vouchers/{id}/purge |  |
| [**accountsetsAccountSetIdVouchersIdPut**](VouchersApi.md#accountsetsaccountsetidvouchersidput) | **PUT** /accountsets/{accountSetId}/Vouchers/{id} |  |
| [**accountsetsAccountSetIdVouchersIdRestorePost**](VouchersApi.md#accountsetsaccountsetidvouchersidrestorepost) | **POST** /accountsets/{accountSetId}/Vouchers/{id}/restore |  |
| [**accountsetsAccountSetIdVouchersIdReversePost**](VouchersApi.md#accountsetsaccountsetidvouchersidreversepost) | **POST** /accountsets/{accountSetId}/Vouchers/{id}/reverse |  |
| [**accountsetsAccountSetIdVouchersIdReviewPost**](VouchersApi.md#accountsetsaccountsetidvouchersidreviewpost) | **POST** /accountsets/{accountSetId}/Vouchers/{id}/review |  |
| [**accountsetsAccountSetIdVouchersIdUnpostPost**](VouchersApi.md#accountsetsaccountsetidvouchersidunpostpost) | **POST** /accountsets/{accountSetId}/Vouchers/{id}/unpost |  |
| [**accountsetsAccountSetIdVouchersIdUnreviewPost**](VouchersApi.md#accountsetsaccountsetidvouchersidunreviewpost) | **POST** /accountsets/{accountSetId}/Vouchers/{id}/unreview |  |
| [**accountsetsAccountSetIdVouchersImportPost**](VouchersApi.md#accountsetsaccountsetidvouchersimportpost) | **POST** /accountsets/{accountSetId}/Vouchers/import |  |
| [**accountsetsAccountSetIdVouchersPdfPost**](VouchersApi.md#accountsetsaccountsetidvoucherspdfpost) | **POST** /accountsets/{accountSetId}/Vouchers/pdf |  |
| [**accountsetsAccountSetIdVouchersPost**](VouchersApi.md#accountsetsaccountsetidvoucherspost) | **POST** /accountsets/{accountSetId}/Vouchers |  |
| [**accountsetsAccountSetIdVouchersRecycleBinDelete**](VouchersApi.md#accountsetsaccountsetidvouchersrecyclebindelete) | **DELETE** /accountsets/{accountSetId}/Vouchers/recycle-bin |  |
| [**accountsetsAccountSetIdVouchersRenumberPost**](VouchersApi.md#accountsetsaccountsetidvouchersrenumberpost) | **POST** /accountsets/{accountSetId}/Vouchers/renumber |  |
| [**accountsetsAccountSetIdVouchersSummariesGet**](VouchersApi.md#accountsetsaccountsetidvoucherssummariesget) | **GET** /accountsets/{accountSetId}/Vouchers/summaries |  |



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


## accountsetsAccountSetIdVouchersExportPost

> accountsetsAccountSetIdVouchersExportPost(accountSetId, exportVouchersRequest)



### Example

```ts
import {
  Configuration,
  VouchersApi,
} from '';
import type { AccountsetsAccountSetIdVouchersExportPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VouchersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // ExportVouchersRequest
    exportVouchersRequest: ...,
  } satisfies AccountsetsAccountSetIdVouchersExportPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchersExportPost(body);
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
| **exportVouchersRequest** | [ExportVouchersRequest](ExportVouchersRequest.md) |  | |

### Return type

`void` (Empty response body)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: `application/json`, `text/json`, `application/*+json`
- **Accept**: Not defined


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## accountsetsAccountSetIdVouchersGet

> Array&lt;VoucherListItemDto&gt; accountsetsAccountSetIdVouchersGet(accountSetId, startDate, endDate, word, preparer, reviewed, hasAssists, summary, remark, accountId, direction, amountFrom, amountTo, numberFrom, numberTo, sortBy, sortDesc, deleted)



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
    // string (optional)
    word: word_example,
    // string (optional)
    preparer: preparer_example,
    // boolean (optional)
    reviewed: true,
    // boolean (optional)
    hasAssists: true,
    // string (optional)
    summary: summary_example,
    // string (optional)
    remark: remark_example,
    // string (optional)
    accountId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // boolean (optional)
    direction: true,
    // number (optional)
    amountFrom: 1.2,
    // number (optional)
    amountTo: 1.2,
    // number (optional)
    numberFrom: 56,
    // number (optional)
    numberTo: 56,
    // string (optional)
    sortBy: sortBy_example,
    // boolean (optional)
    sortDesc: true,
    // boolean (optional)
    deleted: true,
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
| **word** | `string` |  | [Optional] [Defaults to `undefined`] |
| **preparer** | `string` |  | [Optional] [Defaults to `undefined`] |
| **reviewed** | `boolean` |  | [Optional] [Defaults to `undefined`] |
| **hasAssists** | `boolean` |  | [Optional] [Defaults to `undefined`] |
| **summary** | `string` |  | [Optional] [Defaults to `undefined`] |
| **remark** | `string` |  | [Optional] [Defaults to `undefined`] |
| **accountId** | `string` |  | [Optional] [Defaults to `undefined`] |
| **direction** | `boolean` |  | [Optional] [Defaults to `undefined`] |
| **amountFrom** | `number` |  | [Optional] [Defaults to `undefined`] |
| **amountTo** | `number` |  | [Optional] [Defaults to `undefined`] |
| **numberFrom** | `number` |  | [Optional] [Defaults to `undefined`] |
| **numberTo** | `number` |  | [Optional] [Defaults to `undefined`] |
| **sortBy** | `string` |  | [Optional] [Defaults to `undefined`] |
| **sortDesc** | `boolean` |  | [Optional] [Defaults to `false`] |
| **deleted** | `boolean` |  | [Optional] [Defaults to `false`] |

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


## accountsetsAccountSetIdVouchersIdAttachmentsAttachmentIdDelete

> accountsetsAccountSetIdVouchersIdAttachmentsAttachmentIdDelete(accountSetId, id, attachmentId)



### Example

```ts
import {
  Configuration,
  VouchersApi,
} from '';
import type { AccountsetsAccountSetIdVouchersIdAttachmentsAttachmentIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VouchersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    attachmentId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdVouchersIdAttachmentsAttachmentIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchersIdAttachmentsAttachmentIdDelete(body);
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
| **attachmentId** | `string` |  | [Defaults to `undefined`] |

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


## accountsetsAccountSetIdVouchersIdAttachmentsAttachmentIdFileGet

> accountsetsAccountSetIdVouchersIdAttachmentsAttachmentIdFileGet(accountSetId, id, attachmentId, download)



### Example

```ts
import {
  Configuration,
  VouchersApi,
} from '';
import type { AccountsetsAccountSetIdVouchersIdAttachmentsAttachmentIdFileGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VouchersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    attachmentId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // boolean (optional)
    download: true,
  } satisfies AccountsetsAccountSetIdVouchersIdAttachmentsAttachmentIdFileGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchersIdAttachmentsAttachmentIdFileGet(body);
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
| **attachmentId** | `string` |  | [Defaults to `undefined`] |
| **download** | `boolean` |  | [Optional] [Defaults to `false`] |

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


## accountsetsAccountSetIdVouchersIdAttachmentsGet

> Array&lt;VoucherAttachmentDto&gt; accountsetsAccountSetIdVouchersIdAttachmentsGet(accountSetId, id)



### Example

```ts
import {
  Configuration,
  VouchersApi,
} from '';
import type { AccountsetsAccountSetIdVouchersIdAttachmentsGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VouchersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdVouchersIdAttachmentsGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchersIdAttachmentsGet(body);
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

[**Array&lt;VoucherAttachmentDto&gt;**](VoucherAttachmentDto.md)

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


## accountsetsAccountSetIdVouchersIdAttachmentsPost

> VoucherAttachmentDto accountsetsAccountSetIdVouchersIdAttachmentsPost(accountSetId, id, file)



### Example

```ts
import {
  Configuration,
  VouchersApi,
} from '';
import type { AccountsetsAccountSetIdVouchersIdAttachmentsPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VouchersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Blob (optional)
    file: BINARY_DATA_HERE,
  } satisfies AccountsetsAccountSetIdVouchersIdAttachmentsPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchersIdAttachmentsPost(body);
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

[**VoucherAttachmentDto**](VoucherAttachmentDto.md)

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


## accountsetsAccountSetIdVouchersIdCopyPost

> VoucherDetailDto accountsetsAccountSetIdVouchersIdCopyPost(accountSetId, id, date)



### Example

```ts
import {
  Configuration,
  VouchersApi,
} from '';
import type { AccountsetsAccountSetIdVouchersIdCopyPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VouchersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Date (optional)
    date: 2013-10-20,
  } satisfies AccountsetsAccountSetIdVouchersIdCopyPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchersIdCopyPost(body);
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
| **date** | `Date` |  | [Optional] [Defaults to `undefined`] |

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


## accountsetsAccountSetIdVouchersIdPdfGet

> accountsetsAccountSetIdVouchersIdPdfGet(accountSetId, id, paper, orientation, valign, rows, fontSize, mergePrint, showAccountCode)



### Example

```ts
import {
  Configuration,
  VouchersApi,
} from '';
import type { AccountsetsAccountSetIdVouchersIdPdfGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VouchersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string (optional)
    paper: paper_example,
    // string (optional)
    orientation: orientation_example,
    // string (optional)
    valign: valign_example,
    // number (optional)
    rows: 56,
    // number (optional)
    fontSize: 56,
    // boolean (optional)
    mergePrint: true,
    // boolean (optional)
    showAccountCode: true,
  } satisfies AccountsetsAccountSetIdVouchersIdPdfGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchersIdPdfGet(body);
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
| **paper** | `string` |  | [Optional] [Defaults to `&#39;A4x2&#39;`] |
| **orientation** | `string` |  | [Optional] [Defaults to `&#39;portrait&#39;`] |
| **valign** | `string` |  | [Optional] [Defaults to `&#39;top&#39;`] |
| **rows** | `number` |  | [Optional] [Defaults to `5`] |
| **fontSize** | `number` |  | [Optional] [Defaults to `10`] |
| **mergePrint** | `boolean` |  | [Optional] [Defaults to `false`] |
| **showAccountCode** | `boolean` |  | [Optional] [Defaults to `true`] |

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


## accountsetsAccountSetIdVouchersIdPurgeDelete

> accountsetsAccountSetIdVouchersIdPurgeDelete(accountSetId, id)



### Example

```ts
import {
  Configuration,
  VouchersApi,
} from '';
import type { AccountsetsAccountSetIdVouchersIdPurgeDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VouchersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdVouchersIdPurgeDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchersIdPurgeDelete(body);
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


## accountsetsAccountSetIdVouchersIdRestorePost

> VoucherDetailDto accountsetsAccountSetIdVouchersIdRestorePost(accountSetId, id)



### Example

```ts
import {
  Configuration,
  VouchersApi,
} from '';
import type { AccountsetsAccountSetIdVouchersIdRestorePostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VouchersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdVouchersIdRestorePostRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchersIdRestorePost(body);
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


## accountsetsAccountSetIdVouchersIdReversePost

> VoucherDetailDto accountsetsAccountSetIdVouchersIdReversePost(accountSetId, id, date)



### Example

```ts
import {
  Configuration,
  VouchersApi,
} from '';
import type { AccountsetsAccountSetIdVouchersIdReversePostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VouchersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Date (optional)
    date: 2013-10-20,
  } satisfies AccountsetsAccountSetIdVouchersIdReversePostRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchersIdReversePost(body);
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
| **date** | `Date` |  | [Optional] [Defaults to `undefined`] |

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


## accountsetsAccountSetIdVouchersIdUnpostPost

> VoucherDetailDto accountsetsAccountSetIdVouchersIdUnpostPost(accountSetId, id)



### Example

```ts
import {
  Configuration,
  VouchersApi,
} from '';
import type { AccountsetsAccountSetIdVouchersIdUnpostPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VouchersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdVouchersIdUnpostPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchersIdUnpostPost(body);
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


## accountsetsAccountSetIdVouchersPdfPost

> accountsetsAccountSetIdVouchersPdfPost(accountSetId, voucherPdfRequest)



### Example

```ts
import {
  Configuration,
  VouchersApi,
} from '';
import type { AccountsetsAccountSetIdVouchersPdfPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VouchersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // VoucherPdfRequest
    voucherPdfRequest: ...,
  } satisfies AccountsetsAccountSetIdVouchersPdfPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchersPdfPost(body);
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
| **voucherPdfRequest** | [VoucherPdfRequest](VoucherPdfRequest.md) |  | |

### Return type

`void` (Empty response body)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: `application/json`, `text/json`, `application/*+json`
- **Accept**: Not defined


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


## accountsetsAccountSetIdVouchersRecycleBinDelete

> accountsetsAccountSetIdVouchersRecycleBinDelete(accountSetId)



### Example

```ts
import {
  Configuration,
  VouchersApi,
} from '';
import type { AccountsetsAccountSetIdVouchersRecycleBinDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VouchersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdVouchersRecycleBinDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchersRecycleBinDelete(body);
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


## accountsetsAccountSetIdVouchersRenumberPost

> accountsetsAccountSetIdVouchersRenumberPost(accountSetId, renumberVouchersRequest)



### Example

```ts
import {
  Configuration,
  VouchersApi,
} from '';
import type { AccountsetsAccountSetIdVouchersRenumberPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VouchersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // RenumberVouchersRequest
    renumberVouchersRequest: ...,
  } satisfies AccountsetsAccountSetIdVouchersRenumberPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchersRenumberPost(body);
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
| **renumberVouchersRequest** | [RenumberVouchersRequest](RenumberVouchersRequest.md) |  | |

### Return type

`void` (Empty response body)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: `application/json`, `text/json`, `application/*+json`
- **Accept**: Not defined


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## accountsetsAccountSetIdVouchersSummariesGet

> Array&lt;string&gt; accountsetsAccountSetIdVouchersSummariesGet(accountSetId, limit)



### Example

```ts
import {
  Configuration,
  VouchersApi,
} from '';
import type { AccountsetsAccountSetIdVouchersSummariesGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VouchersApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    limit: 56,
  } satisfies AccountsetsAccountSetIdVouchersSummariesGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchersSummariesGet(body);
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
| **limit** | `number` |  | [Optional] [Defaults to `80`] |

### Return type

**Array<string>**

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

