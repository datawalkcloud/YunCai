# VoucherTemplatesApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdVouchertemplatesExportGet**](VoucherTemplatesApi.md#accountsetsaccountsetidvouchertemplatesexportget) | **GET** /accountsets/{accountSetId}/vouchertemplates/export |  |
| [**accountsetsAccountSetIdVouchertemplatesGet**](VoucherTemplatesApi.md#accountsetsaccountsetidvouchertemplatesget) | **GET** /accountsets/{accountSetId}/vouchertemplates |  |
| [**accountsetsAccountSetIdVouchertemplatesIdDelete**](VoucherTemplatesApi.md#accountsetsaccountsetidvouchertemplatesiddelete) | **DELETE** /accountsets/{accountSetId}/vouchertemplates/{id} |  |
| [**accountsetsAccountSetIdVouchertemplatesIdPut**](VoucherTemplatesApi.md#accountsetsaccountsetidvouchertemplatesidput) | **PUT** /accountsets/{accountSetId}/vouchertemplates/{id} |  |
| [**accountsetsAccountSetIdVouchertemplatesImportPost**](VoucherTemplatesApi.md#accountsetsaccountsetidvouchertemplatesimportpost) | **POST** /accountsets/{accountSetId}/vouchertemplates/import |  |
| [**accountsetsAccountSetIdVouchertemplatesImportTemplateGet**](VoucherTemplatesApi.md#accountsetsaccountsetidvouchertemplatesimporttemplateget) | **GET** /accountsets/{accountSetId}/vouchertemplates/import-template |  |
| [**accountsetsAccountSetIdVouchertemplatesPost**](VoucherTemplatesApi.md#accountsetsaccountsetidvouchertemplatespost) | **POST** /accountsets/{accountSetId}/vouchertemplates |  |



## accountsetsAccountSetIdVouchertemplatesExportGet

> accountsetsAccountSetIdVouchertemplatesExportGet(accountSetId)



### Example

```ts
import {
  Configuration,
  VoucherTemplatesApi,
} from '';
import type { AccountsetsAccountSetIdVouchertemplatesExportGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VoucherTemplatesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdVouchertemplatesExportGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchertemplatesExportGet(body);
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


## accountsetsAccountSetIdVouchertemplatesGet

> Array&lt;VoucherTemplateDto&gt; accountsetsAccountSetIdVouchertemplatesGet(accountSetId)



### Example

```ts
import {
  Configuration,
  VoucherTemplatesApi,
} from '';
import type { AccountsetsAccountSetIdVouchertemplatesGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VoucherTemplatesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdVouchertemplatesGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchertemplatesGet(body);
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

[**Array&lt;VoucherTemplateDto&gt;**](VoucherTemplateDto.md)

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


## accountsetsAccountSetIdVouchertemplatesIdDelete

> accountsetsAccountSetIdVouchertemplatesIdDelete(accountSetId, id)



### Example

```ts
import {
  Configuration,
  VoucherTemplatesApi,
} from '';
import type { AccountsetsAccountSetIdVouchertemplatesIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VoucherTemplatesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdVouchertemplatesIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchertemplatesIdDelete(body);
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


## accountsetsAccountSetIdVouchertemplatesIdPut

> VoucherTemplateDto accountsetsAccountSetIdVouchertemplatesIdPut(accountSetId, id, saveVoucherTemplateRequest)



### Example

```ts
import {
  Configuration,
  VoucherTemplatesApi,
} from '';
import type { AccountsetsAccountSetIdVouchertemplatesIdPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VoucherTemplatesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveVoucherTemplateRequest
    saveVoucherTemplateRequest: ...,
  } satisfies AccountsetsAccountSetIdVouchertemplatesIdPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchertemplatesIdPut(body);
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
| **saveVoucherTemplateRequest** | [SaveVoucherTemplateRequest](SaveVoucherTemplateRequest.md) |  | |

### Return type

[**VoucherTemplateDto**](VoucherTemplateDto.md)

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


## accountsetsAccountSetIdVouchertemplatesImportPost

> VoucherTemplateImportResultDto accountsetsAccountSetIdVouchertemplatesImportPost(accountSetId, file)



### Example

```ts
import {
  Configuration,
  VoucherTemplatesApi,
} from '';
import type { AccountsetsAccountSetIdVouchertemplatesImportPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VoucherTemplatesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Blob (optional)
    file: BINARY_DATA_HERE,
  } satisfies AccountsetsAccountSetIdVouchertemplatesImportPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchertemplatesImportPost(body);
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

[**VoucherTemplateImportResultDto**](VoucherTemplateImportResultDto.md)

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


## accountsetsAccountSetIdVouchertemplatesImportTemplateGet

> accountsetsAccountSetIdVouchertemplatesImportTemplateGet(accountSetId)



### Example

```ts
import {
  Configuration,
  VoucherTemplatesApi,
} from '';
import type { AccountsetsAccountSetIdVouchertemplatesImportTemplateGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VoucherTemplatesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdVouchertemplatesImportTemplateGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchertemplatesImportTemplateGet(body);
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


## accountsetsAccountSetIdVouchertemplatesPost

> VoucherTemplateDto accountsetsAccountSetIdVouchertemplatesPost(accountSetId, saveVoucherTemplateRequest)



### Example

```ts
import {
  Configuration,
  VoucherTemplatesApi,
} from '';
import type { AccountsetsAccountSetIdVouchertemplatesPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new VoucherTemplatesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveVoucherTemplateRequest
    saveVoucherTemplateRequest: ...,
  } satisfies AccountsetsAccountSetIdVouchertemplatesPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdVouchertemplatesPost(body);
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
| **saveVoucherTemplateRequest** | [SaveVoucherTemplateRequest](SaveVoucherTemplateRequest.md) |  | |

### Return type

[**VoucherTemplateDto**](VoucherTemplateDto.md)

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

