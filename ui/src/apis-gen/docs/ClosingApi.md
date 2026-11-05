# ClosingApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdClosingCancelCarryPost**](ClosingApi.md#accountsetsaccountsetidclosingcancelcarrypost) | **POST** /accountsets/{accountSetId}/closing/cancel-carry |  |
| [**accountsetsAccountSetIdClosingCarryForwardPost**](ClosingApi.md#accountsetsaccountsetidclosingcarryforwardpost) | **POST** /accountsets/{accountSetId}/closing/carry-forward |  |
| [**accountsetsAccountSetIdClosingCarryForwardPreviewPost**](ClosingApi.md#accountsetsaccountsetidclosingcarryforwardpreviewpost) | **POST** /accountsets/{accountSetId}/closing/carry-forward/preview |  |
| [**accountsetsAccountSetIdClosingChecksGet**](ClosingApi.md#accountsetsaccountsetidclosingchecksget) | **GET** /accountsets/{accountSetId}/closing/checks |  |
| [**accountsetsAccountSetIdClosingCloseBatchPost**](ClosingApi.md#accountsetsaccountsetidclosingclosebatchpost) | **POST** /accountsets/{accountSetId}/closing/close-batch |  |
| [**accountsetsAccountSetIdClosingClosePost**](ClosingApi.md#accountsetsaccountsetidclosingclosepost) | **POST** /accountsets/{accountSetId}/closing/close |  |
| [**accountsetsAccountSetIdClosingGet**](ClosingApi.md#accountsetsaccountsetidclosingget) | **GET** /accountsets/{accountSetId}/closing |  |
| [**accountsetsAccountSetIdClosingPreCloseGeneratePost**](ClosingApi.md#accountsetsaccountsetidclosingpreclosegeneratepost) | **POST** /accountsets/{accountSetId}/closing/pre-close/generate |  |
| [**accountsetsAccountSetIdClosingPreCloseGet**](ClosingApi.md#accountsetsaccountsetidclosingprecloseget) | **GET** /accountsets/{accountSetId}/closing/pre-close |  |
| [**accountsetsAccountSetIdClosingReopenPost**](ClosingApi.md#accountsetsaccountsetidclosingreopenpost) | **POST** /accountsets/{accountSetId}/closing/reopen |  |
| [**accountsetsAccountSetIdClosingTemplatesGet**](ClosingApi.md#accountsetsaccountsetidclosingtemplatesget) | **GET** /accountsets/{accountSetId}/closing/templates |  |
| [**accountsetsAccountSetIdClosingTemplatesPost**](ClosingApi.md#accountsetsaccountsetidclosingtemplatespost) | **POST** /accountsets/{accountSetId}/closing/templates |  |
| [**accountsetsAccountSetIdClosingTemplatesPreviewGet**](ClosingApi.md#accountsetsaccountsetidclosingtemplatespreviewget) | **GET** /accountsets/{accountSetId}/closing/templates/preview |  |
| [**accountsetsAccountSetIdClosingTemplatesTemplateIdDelete**](ClosingApi.md#accountsetsaccountsetidclosingtemplatestemplateiddelete) | **DELETE** /accountsets/{accountSetId}/closing/templates/{templateId} |  |
| [**accountsetsAccountSetIdClosingTemplatesTemplateIdPut**](ClosingApi.md#accountsetsaccountsetidclosingtemplatestemplateidput) | **PUT** /accountsets/{accountSetId}/closing/templates/{templateId} |  |



## accountsetsAccountSetIdClosingCancelCarryPost

> CancelCarryResultDto accountsetsAccountSetIdClosingCancelCarryPost(accountSetId, closingPeriodRequest)



### Example

```ts
import {
  Configuration,
  ClosingApi,
} from '';
import type { AccountsetsAccountSetIdClosingCancelCarryPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ClosingApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // ClosingPeriodRequest
    closingPeriodRequest: ...,
  } satisfies AccountsetsAccountSetIdClosingCancelCarryPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdClosingCancelCarryPost(body);
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
| **closingPeriodRequest** | [ClosingPeriodRequest](ClosingPeriodRequest.md) |  | |

### Return type

[**CancelCarryResultDto**](CancelCarryResultDto.md)

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


## accountsetsAccountSetIdClosingCarryForwardPost

> CarryForwardResultDto accountsetsAccountSetIdClosingCarryForwardPost(accountSetId, carryForwardRequest)



### Example

```ts
import {
  Configuration,
  ClosingApi,
} from '';
import type { AccountsetsAccountSetIdClosingCarryForwardPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ClosingApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // CarryForwardRequest
    carryForwardRequest: ...,
  } satisfies AccountsetsAccountSetIdClosingCarryForwardPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdClosingCarryForwardPost(body);
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
| **carryForwardRequest** | [CarryForwardRequest](CarryForwardRequest.md) |  | |

### Return type

[**CarryForwardResultDto**](CarryForwardResultDto.md)

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


## accountsetsAccountSetIdClosingCarryForwardPreviewPost

> CarryForwardPreviewDto accountsetsAccountSetIdClosingCarryForwardPreviewPost(accountSetId, carryForwardRequest)



### Example

```ts
import {
  Configuration,
  ClosingApi,
} from '';
import type { AccountsetsAccountSetIdClosingCarryForwardPreviewPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ClosingApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // CarryForwardRequest
    carryForwardRequest: ...,
  } satisfies AccountsetsAccountSetIdClosingCarryForwardPreviewPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdClosingCarryForwardPreviewPost(body);
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
| **carryForwardRequest** | [CarryForwardRequest](CarryForwardRequest.md) |  | |

### Return type

[**CarryForwardPreviewDto**](CarryForwardPreviewDto.md)

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


## accountsetsAccountSetIdClosingChecksGet

> ClosingCheckResultDto accountsetsAccountSetIdClosingChecksGet(accountSetId, year, month)



### Example

```ts
import {
  Configuration,
  ClosingApi,
} from '';
import type { AccountsetsAccountSetIdClosingChecksGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ClosingApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    year: 56,
    // number (optional)
    month: 56,
  } satisfies AccountsetsAccountSetIdClosingChecksGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdClosingChecksGet(body);
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
| **year** | `number` |  | [Optional] [Defaults to `undefined`] |
| **month** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**ClosingCheckResultDto**](ClosingCheckResultDto.md)

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


## accountsetsAccountSetIdClosingCloseBatchPost

> CloseBatchResultDto accountsetsAccountSetIdClosingCloseBatchPost(accountSetId)



### Example

```ts
import {
  Configuration,
  ClosingApi,
} from '';
import type { AccountsetsAccountSetIdClosingCloseBatchPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ClosingApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdClosingCloseBatchPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdClosingCloseBatchPost(body);
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

[**CloseBatchResultDto**](CloseBatchResultDto.md)

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


## accountsetsAccountSetIdClosingClosePost

> ClosingStatusDto accountsetsAccountSetIdClosingClosePost(accountSetId, closingPeriodRequest)



### Example

```ts
import {
  Configuration,
  ClosingApi,
} from '';
import type { AccountsetsAccountSetIdClosingClosePostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ClosingApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // ClosingPeriodRequest
    closingPeriodRequest: ...,
  } satisfies AccountsetsAccountSetIdClosingClosePostRequest;

  try {
    const data = await api.accountsetsAccountSetIdClosingClosePost(body);
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
| **closingPeriodRequest** | [ClosingPeriodRequest](ClosingPeriodRequest.md) |  | |

### Return type

[**ClosingStatusDto**](ClosingStatusDto.md)

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


## accountsetsAccountSetIdClosingGet

> ClosingStatusDto accountsetsAccountSetIdClosingGet(accountSetId)



### Example

```ts
import {
  Configuration,
  ClosingApi,
} from '';
import type { AccountsetsAccountSetIdClosingGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ClosingApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdClosingGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdClosingGet(body);
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

[**ClosingStatusDto**](ClosingStatusDto.md)

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


## accountsetsAccountSetIdClosingPreCloseGeneratePost

> GeneratePreCloseResultDto accountsetsAccountSetIdClosingPreCloseGeneratePost(accountSetId, generatePreCloseRequest)



### Example

```ts
import {
  Configuration,
  ClosingApi,
} from '';
import type { AccountsetsAccountSetIdClosingPreCloseGeneratePostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ClosingApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // GeneratePreCloseRequest
    generatePreCloseRequest: ...,
  } satisfies AccountsetsAccountSetIdClosingPreCloseGeneratePostRequest;

  try {
    const data = await api.accountsetsAccountSetIdClosingPreCloseGeneratePost(body);
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
| **generatePreCloseRequest** | [GeneratePreCloseRequest](GeneratePreCloseRequest.md) |  | |

### Return type

[**GeneratePreCloseResultDto**](GeneratePreCloseResultDto.md)

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


## accountsetsAccountSetIdClosingPreCloseGet

> PreCloseListDto accountsetsAccountSetIdClosingPreCloseGet(accountSetId, year, month)



### Example

```ts
import {
  Configuration,
  ClosingApi,
} from '';
import type { AccountsetsAccountSetIdClosingPreCloseGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ClosingApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    year: 56,
    // number (optional)
    month: 56,
  } satisfies AccountsetsAccountSetIdClosingPreCloseGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdClosingPreCloseGet(body);
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
| **year** | `number` |  | [Optional] [Defaults to `undefined`] |
| **month** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**PreCloseListDto**](PreCloseListDto.md)

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


## accountsetsAccountSetIdClosingReopenPost

> ClosingStatusDto accountsetsAccountSetIdClosingReopenPost(accountSetId, year, month)



### Example

```ts
import {
  Configuration,
  ClosingApi,
} from '';
import type { AccountsetsAccountSetIdClosingReopenPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ClosingApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    year: 56,
    // number (optional)
    month: 56,
  } satisfies AccountsetsAccountSetIdClosingReopenPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdClosingReopenPost(body);
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
| **year** | `number` |  | [Optional] [Defaults to `undefined`] |
| **month** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**ClosingStatusDto**](ClosingStatusDto.md)

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


## accountsetsAccountSetIdClosingTemplatesGet

> Array&lt;ClosingTemplateDto&gt; accountsetsAccountSetIdClosingTemplatesGet(accountSetId)



### Example

```ts
import {
  Configuration,
  ClosingApi,
} from '';
import type { AccountsetsAccountSetIdClosingTemplatesGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ClosingApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdClosingTemplatesGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdClosingTemplatesGet(body);
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

[**Array&lt;ClosingTemplateDto&gt;**](ClosingTemplateDto.md)

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


## accountsetsAccountSetIdClosingTemplatesPost

> ClosingTemplateDto accountsetsAccountSetIdClosingTemplatesPost(accountSetId, saveClosingTemplateRequest)



### Example

```ts
import {
  Configuration,
  ClosingApi,
} from '';
import type { AccountsetsAccountSetIdClosingTemplatesPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ClosingApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveClosingTemplateRequest
    saveClosingTemplateRequest: ...,
  } satisfies AccountsetsAccountSetIdClosingTemplatesPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdClosingTemplatesPost(body);
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
| **saveClosingTemplateRequest** | [SaveClosingTemplateRequest](SaveClosingTemplateRequest.md) |  | |

### Return type

[**ClosingTemplateDto**](ClosingTemplateDto.md)

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


## accountsetsAccountSetIdClosingTemplatesPreviewGet

> ClosingTemplatePreviewDto accountsetsAccountSetIdClosingTemplatesPreviewGet(accountSetId, year, month)



### Example

```ts
import {
  Configuration,
  ClosingApi,
} from '';
import type { AccountsetsAccountSetIdClosingTemplatesPreviewGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ClosingApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    year: 56,
    // number (optional)
    month: 56,
  } satisfies AccountsetsAccountSetIdClosingTemplatesPreviewGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdClosingTemplatesPreviewGet(body);
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
| **year** | `number` |  | [Optional] [Defaults to `undefined`] |
| **month** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**ClosingTemplatePreviewDto**](ClosingTemplatePreviewDto.md)

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


## accountsetsAccountSetIdClosingTemplatesTemplateIdDelete

> accountsetsAccountSetIdClosingTemplatesTemplateIdDelete(accountSetId, templateId)



### Example

```ts
import {
  Configuration,
  ClosingApi,
} from '';
import type { AccountsetsAccountSetIdClosingTemplatesTemplateIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ClosingApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    templateId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdClosingTemplatesTemplateIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdClosingTemplatesTemplateIdDelete(body);
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
| **templateId** | `string` |  | [Defaults to `undefined`] |

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


## accountsetsAccountSetIdClosingTemplatesTemplateIdPut

> ClosingTemplateDto accountsetsAccountSetIdClosingTemplatesTemplateIdPut(accountSetId, templateId, saveClosingTemplateRequest)



### Example

```ts
import {
  Configuration,
  ClosingApi,
} from '';
import type { AccountsetsAccountSetIdClosingTemplatesTemplateIdPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new ClosingApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    templateId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveClosingTemplateRequest
    saveClosingTemplateRequest: ...,
  } satisfies AccountsetsAccountSetIdClosingTemplatesTemplateIdPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdClosingTemplatesTemplateIdPut(body);
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
| **templateId** | `string` |  | [Defaults to `undefined`] |
| **saveClosingTemplateRequest** | [SaveClosingTemplateRequest](SaveClosingTemplateRequest.md) |  | |

### Return type

[**ClosingTemplateDto**](ClosingTemplateDto.md)

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

