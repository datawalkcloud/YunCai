# BillsApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdBillsBulkDeletePost**](BillsApi.md#accountsetsaccountsetidbillsbulkdeletepost) | **POST** /accountsets/{accountSetId}/bills/bulk-delete |  |
| [**accountsetsAccountSetIdBillsExportGet**](BillsApi.md#accountsetsaccountsetidbillsexportget) | **GET** /accountsets/{accountSetId}/bills/export |  |
| [**accountsetsAccountSetIdBillsGet**](BillsApi.md#accountsetsaccountsetidbillsget) | **GET** /accountsets/{accountSetId}/bills |  |
| [**accountsetsAccountSetIdBillsIdAttachmentsAttachmentIdDelete**](BillsApi.md#accountsetsaccountsetidbillsidattachmentsattachmentiddelete) | **DELETE** /accountsets/{accountSetId}/bills/{id}/attachments/{attachmentId} |  |
| [**accountsetsAccountSetIdBillsIdAttachmentsAttachmentIdGet**](BillsApi.md#accountsetsaccountsetidbillsidattachmentsattachmentidget) | **GET** /accountsets/{accountSetId}/bills/{id}/attachments/{attachmentId} |  |
| [**accountsetsAccountSetIdBillsIdAttachmentsGet**](BillsApi.md#accountsetsaccountsetidbillsidattachmentsget) | **GET** /accountsets/{accountSetId}/bills/{id}/attachments |  |
| [**accountsetsAccountSetIdBillsIdAttachmentsPost**](BillsApi.md#accountsetsaccountsetidbillsidattachmentspost) | **POST** /accountsets/{accountSetId}/bills/{id}/attachments |  |
| [**accountsetsAccountSetIdBillsIdCollectPost**](BillsApi.md#accountsetsaccountsetidbillsidcollectpost) | **POST** /accountsets/{accountSetId}/bills/{id}/collect |  |
| [**accountsetsAccountSetIdBillsIdDelete**](BillsApi.md#accountsetsaccountsetidbillsiddelete) | **DELETE** /accountsets/{accountSetId}/bills/{id} |  |
| [**accountsetsAccountSetIdBillsIdEndorsePost**](BillsApi.md#accountsetsaccountsetidbillsidendorsepost) | **POST** /accountsets/{accountSetId}/bills/{id}/endorse |  |
| [**accountsetsAccountSetIdBillsIdPut**](BillsApi.md#accountsetsaccountsetidbillsidput) | **PUT** /accountsets/{accountSetId}/bills/{id} |  |
| [**accountsetsAccountSetIdBillsIdStatusPost**](BillsApi.md#accountsetsaccountsetidbillsidstatuspost) | **POST** /accountsets/{accountSetId}/bills/{id}/status |  |
| [**accountsetsAccountSetIdBillsImportPost**](BillsApi.md#accountsetsaccountsetidbillsimportpost) | **POST** /accountsets/{accountSetId}/bills/import |  |
| [**accountsetsAccountSetIdBillsImportTemplateGet**](BillsApi.md#accountsetsaccountsetidbillsimporttemplateget) | **GET** /accountsets/{accountSetId}/bills/import/template |  |
| [**accountsetsAccountSetIdBillsPost**](BillsApi.md#accountsetsaccountsetidbillspost) | **POST** /accountsets/{accountSetId}/bills |  |
| [**accountsetsAccountSetIdBillsVouchersPost**](BillsApi.md#accountsetsaccountsetidbillsvoucherspost) | **POST** /accountsets/{accountSetId}/bills/vouchers |  |



## accountsetsAccountSetIdBillsBulkDeletePost

> number accountsetsAccountSetIdBillsBulkDeletePost(accountSetId, bulkBillRequest)



### Example

```ts
import {
  Configuration,
  BillsApi,
} from '';
import type { AccountsetsAccountSetIdBillsBulkDeletePostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new BillsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // BulkBillRequest
    bulkBillRequest: ...,
  } satisfies AccountsetsAccountSetIdBillsBulkDeletePostRequest;

  try {
    const data = await api.accountsetsAccountSetIdBillsBulkDeletePost(body);
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
| **bulkBillRequest** | [BulkBillRequest](BulkBillRequest.md) |  | |

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


## accountsetsAccountSetIdBillsExportGet

> accountsetsAccountSetIdBillsExportGet(accountSetId, startDate, endDate)



### Example

```ts
import {
  Configuration,
  BillsApi,
} from '';
import type { AccountsetsAccountSetIdBillsExportGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new BillsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
  } satisfies AccountsetsAccountSetIdBillsExportGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdBillsExportGet(body);
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


## accountsetsAccountSetIdBillsGet

> Array&lt;BillDto&gt; accountsetsAccountSetIdBillsGet(accountSetId, startDate, endDate)



### Example

```ts
import {
  Configuration,
  BillsApi,
} from '';
import type { AccountsetsAccountSetIdBillsGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new BillsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
  } satisfies AccountsetsAccountSetIdBillsGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdBillsGet(body);
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

[**Array&lt;BillDto&gt;**](BillDto.md)

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


## accountsetsAccountSetIdBillsIdAttachmentsAttachmentIdDelete

> accountsetsAccountSetIdBillsIdAttachmentsAttachmentIdDelete(accountSetId, id, attachmentId)



### Example

```ts
import {
  Configuration,
  BillsApi,
} from '';
import type { AccountsetsAccountSetIdBillsIdAttachmentsAttachmentIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new BillsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    attachmentId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdBillsIdAttachmentsAttachmentIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdBillsIdAttachmentsAttachmentIdDelete(body);
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


## accountsetsAccountSetIdBillsIdAttachmentsAttachmentIdGet

> accountsetsAccountSetIdBillsIdAttachmentsAttachmentIdGet(accountSetId, id, attachmentId)



### Example

```ts
import {
  Configuration,
  BillsApi,
} from '';
import type { AccountsetsAccountSetIdBillsIdAttachmentsAttachmentIdGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new BillsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    attachmentId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdBillsIdAttachmentsAttachmentIdGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdBillsIdAttachmentsAttachmentIdGet(body);
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


## accountsetsAccountSetIdBillsIdAttachmentsGet

> Array&lt;BillAttachmentInfo&gt; accountsetsAccountSetIdBillsIdAttachmentsGet(accountSetId, id)



### Example

```ts
import {
  Configuration,
  BillsApi,
} from '';
import type { AccountsetsAccountSetIdBillsIdAttachmentsGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new BillsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdBillsIdAttachmentsGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdBillsIdAttachmentsGet(body);
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

[**Array&lt;BillAttachmentInfo&gt;**](BillAttachmentInfo.md)

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


## accountsetsAccountSetIdBillsIdAttachmentsPost

> BillAttachmentInfo accountsetsAccountSetIdBillsIdAttachmentsPost(accountSetId, id, file)



### Example

```ts
import {
  Configuration,
  BillsApi,
} from '';
import type { AccountsetsAccountSetIdBillsIdAttachmentsPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new BillsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Blob (optional)
    file: BINARY_DATA_HERE,
  } satisfies AccountsetsAccountSetIdBillsIdAttachmentsPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdBillsIdAttachmentsPost(body);
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

[**BillAttachmentInfo**](BillAttachmentInfo.md)

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


## accountsetsAccountSetIdBillsIdCollectPost

> BillDto accountsetsAccountSetIdBillsIdCollectPost(accountSetId, id)



### Example

```ts
import {
  Configuration,
  BillsApi,
} from '';
import type { AccountsetsAccountSetIdBillsIdCollectPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new BillsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdBillsIdCollectPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdBillsIdCollectPost(body);
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

[**BillDto**](BillDto.md)

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


## accountsetsAccountSetIdBillsIdDelete

> accountsetsAccountSetIdBillsIdDelete(accountSetId, id)



### Example

```ts
import {
  Configuration,
  BillsApi,
} from '';
import type { AccountsetsAccountSetIdBillsIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new BillsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdBillsIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdBillsIdDelete(body);
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


## accountsetsAccountSetIdBillsIdEndorsePost

> BillDto accountsetsAccountSetIdBillsIdEndorsePost(accountSetId, id)



### Example

```ts
import {
  Configuration,
  BillsApi,
} from '';
import type { AccountsetsAccountSetIdBillsIdEndorsePostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new BillsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdBillsIdEndorsePostRequest;

  try {
    const data = await api.accountsetsAccountSetIdBillsIdEndorsePost(body);
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

[**BillDto**](BillDto.md)

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


## accountsetsAccountSetIdBillsIdPut

> BillDto accountsetsAccountSetIdBillsIdPut(accountSetId, id, saveBillRequest)



### Example

```ts
import {
  Configuration,
  BillsApi,
} from '';
import type { AccountsetsAccountSetIdBillsIdPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new BillsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveBillRequest
    saveBillRequest: ...,
  } satisfies AccountsetsAccountSetIdBillsIdPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdBillsIdPut(body);
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
| **saveBillRequest** | [SaveBillRequest](SaveBillRequest.md) |  | |

### Return type

[**BillDto**](BillDto.md)

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


## accountsetsAccountSetIdBillsIdStatusPost

> BillDto accountsetsAccountSetIdBillsIdStatusPost(accountSetId, id, changeBillStatusRequest)



### Example

```ts
import {
  Configuration,
  BillsApi,
} from '';
import type { AccountsetsAccountSetIdBillsIdStatusPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new BillsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // ChangeBillStatusRequest
    changeBillStatusRequest: ...,
  } satisfies AccountsetsAccountSetIdBillsIdStatusPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdBillsIdStatusPost(body);
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
| **changeBillStatusRequest** | [ChangeBillStatusRequest](ChangeBillStatusRequest.md) |  | |

### Return type

[**BillDto**](BillDto.md)

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


## accountsetsAccountSetIdBillsImportPost

> BillImportResult accountsetsAccountSetIdBillsImportPost(accountSetId, file)



### Example

```ts
import {
  Configuration,
  BillsApi,
} from '';
import type { AccountsetsAccountSetIdBillsImportPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new BillsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Blob (optional)
    file: BINARY_DATA_HERE,
  } satisfies AccountsetsAccountSetIdBillsImportPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdBillsImportPost(body);
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

[**BillImportResult**](BillImportResult.md)

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


## accountsetsAccountSetIdBillsImportTemplateGet

> accountsetsAccountSetIdBillsImportTemplateGet(accountSetId)



### Example

```ts
import {
  Configuration,
  BillsApi,
} from '';
import type { AccountsetsAccountSetIdBillsImportTemplateGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new BillsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdBillsImportTemplateGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdBillsImportTemplateGet(body);
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


## accountsetsAccountSetIdBillsPost

> BillDto accountsetsAccountSetIdBillsPost(accountSetId, saveBillRequest)



### Example

```ts
import {
  Configuration,
  BillsApi,
} from '';
import type { AccountsetsAccountSetIdBillsPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new BillsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveBillRequest
    saveBillRequest: ...,
  } satisfies AccountsetsAccountSetIdBillsPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdBillsPost(body);
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
| **saveBillRequest** | [SaveBillRequest](SaveBillRequest.md) |  | |

### Return type

[**BillDto**](BillDto.md)

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


## accountsetsAccountSetIdBillsVouchersPost

> BulkBillVoucherResult accountsetsAccountSetIdBillsVouchersPost(accountSetId, bulkBillRequest)



### Example

```ts
import {
  Configuration,
  BillsApi,
} from '';
import type { AccountsetsAccountSetIdBillsVouchersPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new BillsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // BulkBillRequest
    bulkBillRequest: ...,
  } satisfies AccountsetsAccountSetIdBillsVouchersPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdBillsVouchersPost(body);
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
| **bulkBillRequest** | [BulkBillRequest](BulkBillRequest.md) |  | |

### Return type

[**BulkBillVoucherResult**](BulkBillVoucherResult.md)

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

