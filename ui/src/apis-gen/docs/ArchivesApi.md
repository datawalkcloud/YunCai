# ArchivesApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdArchivesArchivePost**](ArchivesApi.md#accountsetsaccountsetidarchivesarchivepost) | **POST** /accountsets/{accountSetId}/archives/archive |  |
| [**accountsetsAccountSetIdArchivesArchivedGet**](ArchivesApi.md#accountsetsaccountsetidarchivesarchivedget) | **GET** /accountsets/{accountSetId}/archives/archived |  |
| [**accountsetsAccountSetIdArchivesAuditPost**](ArchivesApi.md#accountsetsaccountsetidarchivesauditpost) | **POST** /accountsets/{accountSetId}/archives/audit |  |
| [**accountsetsAccountSetIdArchivesGet**](ArchivesApi.md#accountsetsaccountsetidarchivesget) | **GET** /accountsets/{accountSetId}/archives |  |
| [**accountsetsAccountSetIdArchivesIdDelete**](ArchivesApi.md#accountsetsaccountsetidarchivesiddelete) | **DELETE** /accountsets/{accountSetId}/archives/{id} |  |
| [**accountsetsAccountSetIdArchivesIdDownloadGet**](ArchivesApi.md#accountsetsaccountsetidarchivesiddownloadget) | **GET** /accountsets/{accountSetId}/archives/{id}/download |  |
| [**accountsetsAccountSetIdArchivesIdPut**](ArchivesApi.md#accountsetsaccountsetidarchivesidput) | **PUT** /accountsets/{accountSetId}/archives/{id} |  |
| [**accountsetsAccountSetIdArchivesLinkVoucherPost**](ArchivesApi.md#accountsetsaccountsetidarchiveslinkvoucherpost) | **POST** /accountsets/{accountSetId}/archives/link-voucher |  |
| [**accountsetsAccountSetIdArchivesPaperPost**](ArchivesApi.md#accountsetsaccountsetidarchivespaperpost) | **POST** /accountsets/{accountSetId}/archives/paper |  |
| [**accountsetsAccountSetIdArchivesPost**](ArchivesApi.md#accountsetsaccountsetidarchivespost) | **POST** /accountsets/{accountSetId}/archives |  |
| [**accountsetsAccountSetIdArchivesUsageGet**](ArchivesApi.md#accountsetsaccountsetidarchivesusageget) | **GET** /accountsets/{accountSetId}/archives/usage |  |
| [**accountsetsAccountSetIdArchivesVoucherFromPost**](ArchivesApi.md#accountsetsaccountsetidarchivesvoucherfrompost) | **POST** /accountsets/{accountSetId}/archives/voucher-from |  |



## accountsetsAccountSetIdArchivesArchivePost

> number accountsetsAccountSetIdArchivesArchivePost(accountSetId, archiveIdsRequest)



### Example

```ts
import {
  Configuration,
  ArchivesApi,
} from '';
import type { AccountsetsAccountSetIdArchivesArchivePostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ArchivesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // ArchiveIdsRequest
    archiveIdsRequest: ...,
  } satisfies AccountsetsAccountSetIdArchivesArchivePostRequest;

  try {
    const data = await api.accountsetsAccountSetIdArchivesArchivePost(body);
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
| **archiveIdsRequest** | [ArchiveIdsRequest](ArchiveIdsRequest.md) |  | |

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


## accountsetsAccountSetIdArchivesArchivedGet

> Array&lt;ArchiveItemDto&gt; accountsetsAccountSetIdArchivesArchivedGet(accountSetId)



### Example

```ts
import {
  Configuration,
  ArchivesApi,
} from '';
import type { AccountsetsAccountSetIdArchivesArchivedGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ArchivesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdArchivesArchivedGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdArchivesArchivedGet(body);
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

[**Array&lt;ArchiveItemDto&gt;**](ArchiveItemDto.md)

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


## accountsetsAccountSetIdArchivesAuditPost

> number accountsetsAccountSetIdArchivesAuditPost(accountSetId, auditArchiveRequest)



### Example

```ts
import {
  Configuration,
  ArchivesApi,
} from '';
import type { AccountsetsAccountSetIdArchivesAuditPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ArchivesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // AuditArchiveRequest
    auditArchiveRequest: ...,
  } satisfies AccountsetsAccountSetIdArchivesAuditPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdArchivesAuditPost(body);
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
| **auditArchiveRequest** | [AuditArchiveRequest](AuditArchiveRequest.md) |  | |

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


## accountsetsAccountSetIdArchivesGet

> Array&lt;ArchiveItemDto&gt; accountsetsAccountSetIdArchivesGet(accountSetId)



### Example

```ts
import {
  Configuration,
  ArchivesApi,
} from '';
import type { AccountsetsAccountSetIdArchivesGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ArchivesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdArchivesGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdArchivesGet(body);
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

[**Array&lt;ArchiveItemDto&gt;**](ArchiveItemDto.md)

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


## accountsetsAccountSetIdArchivesIdDelete

> accountsetsAccountSetIdArchivesIdDelete(accountSetId, id)



### Example

```ts
import {
  Configuration,
  ArchivesApi,
} from '';
import type { AccountsetsAccountSetIdArchivesIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ArchivesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdArchivesIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdArchivesIdDelete(body);
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


## accountsetsAccountSetIdArchivesIdDownloadGet

> accountsetsAccountSetIdArchivesIdDownloadGet(accountSetId, id)



### Example

```ts
import {
  Configuration,
  ArchivesApi,
} from '';
import type { AccountsetsAccountSetIdArchivesIdDownloadGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ArchivesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdArchivesIdDownloadGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdArchivesIdDownloadGet(body);
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


## accountsetsAccountSetIdArchivesIdPut

> ArchiveItemDto accountsetsAccountSetIdArchivesIdPut(accountSetId, id, saveArchiveRequest)



### Example

```ts
import {
  Configuration,
  ArchivesApi,
} from '';
import type { AccountsetsAccountSetIdArchivesIdPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ArchivesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveArchiveRequest
    saveArchiveRequest: ...,
  } satisfies AccountsetsAccountSetIdArchivesIdPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdArchivesIdPut(body);
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
| **saveArchiveRequest** | [SaveArchiveRequest](SaveArchiveRequest.md) |  | |

### Return type

[**ArchiveItemDto**](ArchiveItemDto.md)

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


## accountsetsAccountSetIdArchivesLinkVoucherPost

> number accountsetsAccountSetIdArchivesLinkVoucherPost(accountSetId, linkVoucherRequest)



### Example

```ts
import {
  Configuration,
  ArchivesApi,
} from '';
import type { AccountsetsAccountSetIdArchivesLinkVoucherPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ArchivesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // LinkVoucherRequest
    linkVoucherRequest: ...,
  } satisfies AccountsetsAccountSetIdArchivesLinkVoucherPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdArchivesLinkVoucherPost(body);
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
| **linkVoucherRequest** | [LinkVoucherRequest](LinkVoucherRequest.md) |  | |

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


## accountsetsAccountSetIdArchivesPaperPost

> number accountsetsAccountSetIdArchivesPaperPost(accountSetId, paperArchiveRequest)



### Example

```ts
import {
  Configuration,
  ArchivesApi,
} from '';
import type { AccountsetsAccountSetIdArchivesPaperPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ArchivesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // PaperArchiveRequest
    paperArchiveRequest: ...,
  } satisfies AccountsetsAccountSetIdArchivesPaperPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdArchivesPaperPost(body);
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
| **paperArchiveRequest** | [PaperArchiveRequest](PaperArchiveRequest.md) |  | |

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


## accountsetsAccountSetIdArchivesPost

> ArchiveItemDto accountsetsAccountSetIdArchivesPost(accountSetId, saveArchiveRequest)



### Example

```ts
import {
  Configuration,
  ArchivesApi,
} from '';
import type { AccountsetsAccountSetIdArchivesPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ArchivesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveArchiveRequest
    saveArchiveRequest: ...,
  } satisfies AccountsetsAccountSetIdArchivesPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdArchivesPost(body);
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
| **saveArchiveRequest** | [SaveArchiveRequest](SaveArchiveRequest.md) |  | |

### Return type

[**ArchiveItemDto**](ArchiveItemDto.md)

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


## accountsetsAccountSetIdArchivesUsageGet

> ArchiveUsageDto accountsetsAccountSetIdArchivesUsageGet(accountSetId)



### Example

```ts
import {
  Configuration,
  ArchivesApi,
} from '';
import type { AccountsetsAccountSetIdArchivesUsageGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ArchivesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdArchivesUsageGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdArchivesUsageGet(body);
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

[**ArchiveUsageDto**](ArchiveUsageDto.md)

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


## accountsetsAccountSetIdArchivesVoucherFromPost

> accountsetsAccountSetIdArchivesVoucherFromPost(accountSetId, generateVoucherFromArchivesRequest)



### Example

```ts
import {
  Configuration,
  ArchivesApi,
} from '';
import type { AccountsetsAccountSetIdArchivesVoucherFromPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ArchivesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // GenerateVoucherFromArchivesRequest
    generateVoucherFromArchivesRequest: ...,
  } satisfies AccountsetsAccountSetIdArchivesVoucherFromPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdArchivesVoucherFromPost(body);
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
| **generateVoucherFromArchivesRequest** | [GenerateVoucherFromArchivesRequest](GenerateVoucherFromArchivesRequest.md) |  | |

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

