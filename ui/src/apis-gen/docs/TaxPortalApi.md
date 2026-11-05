# TaxPortalApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**taxPortalAccountSetIdBrowserSessionTicketPost**](TaxPortalApi.md#taxportalaccountsetidbrowsersessionticketpost) | **POST** /TaxPortal/{accountSetId}/browser-session-ticket |  |
| [**taxPortalAccountSetIdCertificatesGet**](TaxPortalApi.md#taxportalaccountsetidcertificatesget) | **GET** /TaxPortal/{accountSetId}/certificates |  |
| [**taxPortalAccountSetIdDebugExplorePost**](TaxPortalApi.md#taxportalaccountsetiddebugexplorepost) | **POST** /TaxPortal/{accountSetId}/debug/explore |  |
| [**taxPortalAccountSetIdDebugIitPlainPost**](TaxPortalApi.md#taxportalaccountsetiddebugiitplainpost) | **POST** /TaxPortal/{accountSetId}/debug/iit-plain |  |
| [**taxPortalAccountSetIdDebugInvoiceQueryPost**](TaxPortalApi.md#taxportalaccountsetiddebuginvoicequerypost) | **POST** /TaxPortal/{accountSetId}/debug/invoice-query |  |
| [**taxPortalAccountSetIdDeclarationReportsGet**](TaxPortalApi.md#taxportalaccountsetiddeclarationreportsget) | **GET** /TaxPortal/{accountSetId}/declaration-reports |  |
| [**taxPortalAccountSetIdDeclarationsGet**](TaxPortalApi.md#taxportalaccountsetiddeclarationsget) | **GET** /TaxPortal/{accountSetId}/declarations |  |
| [**taxPortalAccountSetIdIitDeductionsPost**](TaxPortalApi.md#taxportalaccountsetidiitdeductionspost) | **POST** /TaxPortal/{accountSetId}/iit/deductions |  |
| [**taxPortalAccountSetIdIitFilingsPost**](TaxPortalApi.md#taxportalaccountsetidiitfilingspost) | **POST** /TaxPortal/{accountSetId}/iit/filings |  |
| [**taxPortalAccountSetIdIitJobsTaskIdGet**](TaxPortalApi.md#taxportalaccountsetidiitjobstaskidget) | **GET** /TaxPortal/{accountSetId}/iit/jobs/{taskId} |  |
| [**taxPortalAccountSetIdIitLoginPost**](TaxPortalApi.md#taxportalaccountsetidiitloginpost) | **POST** /TaxPortal/{accountSetId}/iit/login |  |
| [**taxPortalAccountSetIdIitLoginStatusGet**](TaxPortalApi.md#taxportalaccountsetidiitloginstatusget) | **GET** /TaxPortal/{accountSetId}/iit/login/status |  |
| [**taxPortalAccountSetIdIitLoginVerifyPost**](TaxPortalApi.md#taxportalaccountsetidiitloginverifypost) | **POST** /TaxPortal/{accountSetId}/iit/login/verify |  |
| [**taxPortalAccountSetIdIitLogoutPost**](TaxPortalApi.md#taxportalaccountsetidiitlogoutpost) | **POST** /TaxPortal/{accountSetId}/iit/logout |  |
| [**taxPortalAccountSetIdIitPaymentsPost**](TaxPortalApi.md#taxportalaccountsetidiitpaymentspost) | **POST** /TaxPortal/{accountSetId}/iit/payments |  |
| [**taxPortalAccountSetIdIitPersonnelPost**](TaxPortalApi.md#taxportalaccountsetidiitpersonnelpost) | **POST** /TaxPortal/{accountSetId}/iit/personnel |  |
| [**taxPortalAccountSetIdIitReportGet**](TaxPortalApi.md#taxportalaccountsetidiitreportget) | **GET** /TaxPortal/{accountSetId}/iit/report |  |
| [**taxPortalAccountSetIdInvoicesSyncPost**](TaxPortalApi.md#taxportalaccountsetidinvoicessyncpost) | **POST** /TaxPortal/{accountSetId}/invoices/sync |  |
| [**taxPortalAccountSetIdInvoicesSyncTaskIdGet**](TaxPortalApi.md#taxportalaccountsetidinvoicessynctaskidget) | **GET** /TaxPortal/{accountSetId}/invoices/sync/{taskId} |  |
| [**taxPortalAccountSetIdLoginCaptchaPost**](TaxPortalApi.md#taxportalaccountsetidlogincaptchapost) | **POST** /TaxPortal/{accountSetId}/login/captcha |  |
| [**taxPortalAccountSetIdLoginPost**](TaxPortalApi.md#taxportalaccountsetidloginpost) | **POST** /TaxPortal/{accountSetId}/login |  |
| [**taxPortalAccountSetIdLoginResumePost**](TaxPortalApi.md#taxportalaccountsetidloginresumepost) | **POST** /TaxPortal/{accountSetId}/login/resume |  |
| [**taxPortalAccountSetIdLoginStatusGet**](TaxPortalApi.md#taxportalaccountsetidloginstatusget) | **GET** /TaxPortal/{accountSetId}/login/status |  |
| [**taxPortalAccountSetIdLoginVerifyPost**](TaxPortalApi.md#taxportalaccountsetidloginverifypost) | **POST** /TaxPortal/{accountSetId}/login/verify |  |
| [**taxPortalAccountSetIdLogoutPost**](TaxPortalApi.md#taxportalaccountsetidlogoutpost) | **POST** /TaxPortal/{accountSetId}/logout |  |
| [**taxPortalAccountSetIdPayablesGet**](TaxPortalApi.md#taxportalaccountsetidpayablesget) | **GET** /TaxPortal/{accountSetId}/payables |  |
| [**taxPortalAccountSetIdPaymentsGet**](TaxPortalApi.md#taxportalaccountsetidpaymentsget) | **GET** /TaxPortal/{accountSetId}/payments |  |
| [**taxPortalAccountSetIdProbeUndeclaredGet**](TaxPortalApi.md#taxportalaccountsetidprobeundeclaredget) | **GET** /TaxPortal/{accountSetId}/probe-undeclared |  |
| [**taxPortalAccountSetIdTasksGet**](TaxPortalApi.md#taxportalaccountsetidtasksget) | **GET** /TaxPortal/{accountSetId}/tasks |  |
| [**taxPortalAccountSetIdTaxKindsGet**](TaxPortalApi.md#taxportalaccountsetidtaxkindsget) | **GET** /TaxPortal/{accountSetId}/tax-kinds |  |



## taxPortalAccountSetIdBrowserSessionTicketPost

> TaxPortalBrowserTicketDto taxPortalAccountSetIdBrowserSessionTicketPost(accountSetId)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdBrowserSessionTicketPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies TaxPortalAccountSetIdBrowserSessionTicketPostRequest;

  try {
    const data = await api.taxPortalAccountSetIdBrowserSessionTicketPost(body);
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

[**TaxPortalBrowserTicketDto**](TaxPortalBrowserTicketDto.md)

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


## taxPortalAccountSetIdCertificatesGet

> TaxCertificatesResultDto taxPortalAccountSetIdCertificatesGet(accountSetId, from, to)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdCertificatesGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Date (optional)
    from: 2013-10-20,
    // Date (optional)
    to: 2013-10-20,
  } satisfies TaxPortalAccountSetIdCertificatesGetRequest;

  try {
    const data = await api.taxPortalAccountSetIdCertificatesGet(body);
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
| **from** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **to** | `Date` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**TaxCertificatesResultDto**](TaxCertificatesResultDto.md)

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


## taxPortalAccountSetIdDebugExplorePost

> taxPortalAccountSetIdDebugExplorePost(accountSetId, exploreRequest)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdDebugExplorePostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // ExploreRequest
    exploreRequest: ...,
  } satisfies TaxPortalAccountSetIdDebugExplorePostRequest;

  try {
    const data = await api.taxPortalAccountSetIdDebugExplorePost(body);
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
| **exploreRequest** | [ExploreRequest](ExploreRequest.md) |  | |

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


## taxPortalAccountSetIdDebugIitPlainPost

> taxPortalAccountSetIdDebugIitPlainPost(accountSetId, iitPlainProbeRequest)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdDebugIitPlainPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // IitPlainProbeRequest (optional)
    iitPlainProbeRequest: ...,
  } satisfies TaxPortalAccountSetIdDebugIitPlainPostRequest;

  try {
    const data = await api.taxPortalAccountSetIdDebugIitPlainPost(body);
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
| **iitPlainProbeRequest** | [IitPlainProbeRequest](IitPlainProbeRequest.md) |  | [Optional] |

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


## taxPortalAccountSetIdDebugInvoiceQueryPost

> taxPortalAccountSetIdDebugInvoiceQueryPost(accountSetId, invoiceQueryProbeRequest)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdDebugInvoiceQueryPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // InvoiceQueryProbeRequest
    invoiceQueryProbeRequest: ...,
  } satisfies TaxPortalAccountSetIdDebugInvoiceQueryPostRequest;

  try {
    const data = await api.taxPortalAccountSetIdDebugInvoiceQueryPost(body);
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
| **invoiceQueryProbeRequest** | [InvoiceQueryProbeRequest](InvoiceQueryProbeRequest.md) |  | |

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


## taxPortalAccountSetIdDeclarationReportsGet

> TaxDeclarationsResultDto taxPortalAccountSetIdDeclarationReportsGet(accountSetId, from, to)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdDeclarationReportsGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Date (optional)
    from: 2013-10-20,
    // Date (optional)
    to: 2013-10-20,
  } satisfies TaxPortalAccountSetIdDeclarationReportsGetRequest;

  try {
    const data = await api.taxPortalAccountSetIdDeclarationReportsGet(body);
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
| **from** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **to** | `Date` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**TaxDeclarationsResultDto**](TaxDeclarationsResultDto.md)

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


## taxPortalAccountSetIdDeclarationsGet

> TaxDeclarationsResultDto taxPortalAccountSetIdDeclarationsGet(accountSetId, period)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdDeclarationsGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string (optional)
    period: period_example,
  } satisfies TaxPortalAccountSetIdDeclarationsGetRequest;

  try {
    const data = await api.taxPortalAccountSetIdDeclarationsGet(body);
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
| **period** | `string` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**TaxDeclarationsResultDto**](TaxDeclarationsResultDto.md)

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


## taxPortalAccountSetIdIitDeductionsPost

> TaxIitJobDto taxPortalAccountSetIdIitDeductionsPost(accountSetId)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdIitDeductionsPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies TaxPortalAccountSetIdIitDeductionsPostRequest;

  try {
    const data = await api.taxPortalAccountSetIdIitDeductionsPost(body);
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

[**TaxIitJobDto**](TaxIitJobDto.md)

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


## taxPortalAccountSetIdIitFilingsPost

> TaxIitJobDto taxPortalAccountSetIdIitFilingsPost(accountSetId, start, end)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdIitFilingsPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string (optional)
    start: start_example,
    // string (optional)
    end: end_example,
  } satisfies TaxPortalAccountSetIdIitFilingsPostRequest;

  try {
    const data = await api.taxPortalAccountSetIdIitFilingsPost(body);
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
| **start** | `string` |  | [Optional] [Defaults to `undefined`] |
| **end** | `string` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**TaxIitJobDto**](TaxIitJobDto.md)

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


## taxPortalAccountSetIdIitJobsTaskIdGet

> TaxIitJobDto taxPortalAccountSetIdIitJobsTaskIdGet(accountSetId, taskId)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdIitJobsTaskIdGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    taskId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies TaxPortalAccountSetIdIitJobsTaskIdGetRequest;

  try {
    const data = await api.taxPortalAccountSetIdIitJobsTaskIdGet(body);
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
| **taskId** | `string` |  | [Defaults to `undefined`] |

### Return type

[**TaxIitJobDto**](TaxIitJobDto.md)

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


## taxPortalAccountSetIdIitLoginPost

> TaxPortalLoginStatusDto taxPortalAccountSetIdIitLoginPost(accountSetId, taxIitLoginRequest)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdIitLoginPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // TaxIitLoginRequest (optional)
    taxIitLoginRequest: ...,
  } satisfies TaxPortalAccountSetIdIitLoginPostRequest;

  try {
    const data = await api.taxPortalAccountSetIdIitLoginPost(body);
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
| **taxIitLoginRequest** | [TaxIitLoginRequest](TaxIitLoginRequest.md) |  | [Optional] |

### Return type

[**TaxPortalLoginStatusDto**](TaxPortalLoginStatusDto.md)

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


## taxPortalAccountSetIdIitLoginStatusGet

> TaxPortalLoginStatusDto taxPortalAccountSetIdIitLoginStatusGet(accountSetId)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdIitLoginStatusGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies TaxPortalAccountSetIdIitLoginStatusGetRequest;

  try {
    const data = await api.taxPortalAccountSetIdIitLoginStatusGet(body);
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

[**TaxPortalLoginStatusDto**](TaxPortalLoginStatusDto.md)

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


## taxPortalAccountSetIdIitLoginVerifyPost

> TaxPortalLoginStatusDto taxPortalAccountSetIdIitLoginVerifyPost(accountSetId, taxPortalVerifyRequest)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdIitLoginVerifyPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // TaxPortalVerifyRequest
    taxPortalVerifyRequest: ...,
  } satisfies TaxPortalAccountSetIdIitLoginVerifyPostRequest;

  try {
    const data = await api.taxPortalAccountSetIdIitLoginVerifyPost(body);
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
| **taxPortalVerifyRequest** | [TaxPortalVerifyRequest](TaxPortalVerifyRequest.md) |  | |

### Return type

[**TaxPortalLoginStatusDto**](TaxPortalLoginStatusDto.md)

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


## taxPortalAccountSetIdIitLogoutPost

> taxPortalAccountSetIdIitLogoutPost(accountSetId)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdIitLogoutPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies TaxPortalAccountSetIdIitLogoutPostRequest;

  try {
    const data = await api.taxPortalAccountSetIdIitLogoutPost(body);
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


## taxPortalAccountSetIdIitPaymentsPost

> TaxIitJobDto taxPortalAccountSetIdIitPaymentsPost(accountSetId, dateField, start, end)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdIitPaymentsPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string (optional)
    dateField: dateField_example,
    // string (optional)
    start: start_example,
    // string (optional)
    end: end_example,
  } satisfies TaxPortalAccountSetIdIitPaymentsPostRequest;

  try {
    const data = await api.taxPortalAccountSetIdIitPaymentsPost(body);
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
| **dateField** | `string` |  | [Optional] [Defaults to `undefined`] |
| **start** | `string` |  | [Optional] [Defaults to `undefined`] |
| **end** | `string` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**TaxIitJobDto**](TaxIitJobDto.md)

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


## taxPortalAccountSetIdIitPersonnelPost

> TaxIitJobDto taxPortalAccountSetIdIitPersonnelPost(accountSetId)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdIitPersonnelPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies TaxPortalAccountSetIdIitPersonnelPostRequest;

  try {
    const data = await api.taxPortalAccountSetIdIitPersonnelPost(body);
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

[**TaxIitJobDto**](TaxIitJobDto.md)

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


## taxPortalAccountSetIdIitReportGet

> TaxIitReportResultDto taxPortalAccountSetIdIitReportGet(accountSetId, period)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdIitReportGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string (optional)
    period: period_example,
  } satisfies TaxPortalAccountSetIdIitReportGetRequest;

  try {
    const data = await api.taxPortalAccountSetIdIitReportGet(body);
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
| **period** | `string` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**TaxIitReportResultDto**](TaxIitReportResultDto.md)

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


## taxPortalAccountSetIdInvoicesSyncPost

> TaxInvoiceSyncJobDto taxPortalAccountSetIdInvoicesSyncPost(accountSetId, startDate, endDate)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdInvoicesSyncPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
  } satisfies TaxPortalAccountSetIdInvoicesSyncPostRequest;

  try {
    const data = await api.taxPortalAccountSetIdInvoicesSyncPost(body);
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

[**TaxInvoiceSyncJobDto**](TaxInvoiceSyncJobDto.md)

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


## taxPortalAccountSetIdInvoicesSyncTaskIdGet

> TaxInvoiceSyncJobDto taxPortalAccountSetIdInvoicesSyncTaskIdGet(accountSetId, taskId)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdInvoicesSyncTaskIdGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    taskId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies TaxPortalAccountSetIdInvoicesSyncTaskIdGetRequest;

  try {
    const data = await api.taxPortalAccountSetIdInvoicesSyncTaskIdGet(body);
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
| **taskId** | `string` |  | [Defaults to `undefined`] |

### Return type

[**TaxInvoiceSyncJobDto**](TaxInvoiceSyncJobDto.md)

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


## taxPortalAccountSetIdLoginCaptchaPost

> TaxPortalLoginStatusDto taxPortalAccountSetIdLoginCaptchaPost(accountSetId, taxPortalCaptchaRequest)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdLoginCaptchaPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // TaxPortalCaptchaRequest
    taxPortalCaptchaRequest: ...,
  } satisfies TaxPortalAccountSetIdLoginCaptchaPostRequest;

  try {
    const data = await api.taxPortalAccountSetIdLoginCaptchaPost(body);
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
| **taxPortalCaptchaRequest** | [TaxPortalCaptchaRequest](TaxPortalCaptchaRequest.md) |  | |

### Return type

[**TaxPortalLoginStatusDto**](TaxPortalLoginStatusDto.md)

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


## taxPortalAccountSetIdLoginPost

> TaxPortalLoginStatusDto taxPortalAccountSetIdLoginPost(accountSetId)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdLoginPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies TaxPortalAccountSetIdLoginPostRequest;

  try {
    const data = await api.taxPortalAccountSetIdLoginPost(body);
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

[**TaxPortalLoginStatusDto**](TaxPortalLoginStatusDto.md)

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


## taxPortalAccountSetIdLoginResumePost

> TaxPortalLoginStatusDto taxPortalAccountSetIdLoginResumePost(accountSetId)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdLoginResumePostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies TaxPortalAccountSetIdLoginResumePostRequest;

  try {
    const data = await api.taxPortalAccountSetIdLoginResumePost(body);
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

[**TaxPortalLoginStatusDto**](TaxPortalLoginStatusDto.md)

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


## taxPortalAccountSetIdLoginStatusGet

> TaxPortalLoginStatusDto taxPortalAccountSetIdLoginStatusGet(accountSetId)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdLoginStatusGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies TaxPortalAccountSetIdLoginStatusGetRequest;

  try {
    const data = await api.taxPortalAccountSetIdLoginStatusGet(body);
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

[**TaxPortalLoginStatusDto**](TaxPortalLoginStatusDto.md)

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


## taxPortalAccountSetIdLoginVerifyPost

> TaxPortalLoginStatusDto taxPortalAccountSetIdLoginVerifyPost(accountSetId, taxPortalVerifyRequest)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdLoginVerifyPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // TaxPortalVerifyRequest
    taxPortalVerifyRequest: ...,
  } satisfies TaxPortalAccountSetIdLoginVerifyPostRequest;

  try {
    const data = await api.taxPortalAccountSetIdLoginVerifyPost(body);
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
| **taxPortalVerifyRequest** | [TaxPortalVerifyRequest](TaxPortalVerifyRequest.md) |  | |

### Return type

[**TaxPortalLoginStatusDto**](TaxPortalLoginStatusDto.md)

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


## taxPortalAccountSetIdLogoutPost

> taxPortalAccountSetIdLogoutPost(accountSetId)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdLogoutPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies TaxPortalAccountSetIdLogoutPostRequest;

  try {
    const data = await api.taxPortalAccountSetIdLogoutPost(body);
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


## taxPortalAccountSetIdPayablesGet

> TaxPayablesResultDto taxPortalAccountSetIdPayablesGet(accountSetId, period)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdPayablesGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string (optional)
    period: period_example,
  } satisfies TaxPortalAccountSetIdPayablesGetRequest;

  try {
    const data = await api.taxPortalAccountSetIdPayablesGet(body);
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
| **period** | `string` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**TaxPayablesResultDto**](TaxPayablesResultDto.md)

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


## taxPortalAccountSetIdPaymentsGet

> TaxPaymentsResultDto taxPortalAccountSetIdPaymentsGet(accountSetId, from, to)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdPaymentsGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Date (optional)
    from: 2013-10-20,
    // Date (optional)
    to: 2013-10-20,
  } satisfies TaxPortalAccountSetIdPaymentsGetRequest;

  try {
    const data = await api.taxPortalAccountSetIdPaymentsGet(body);
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
| **from** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **to** | `Date` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**TaxPaymentsResultDto**](TaxPaymentsResultDto.md)

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


## taxPortalAccountSetIdProbeUndeclaredGet

> TaxDeclarationsResultDto taxPortalAccountSetIdProbeUndeclaredGet(accountSetId)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdProbeUndeclaredGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies TaxPortalAccountSetIdProbeUndeclaredGetRequest;

  try {
    const data = await api.taxPortalAccountSetIdProbeUndeclaredGet(body);
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

[**TaxDeclarationsResultDto**](TaxDeclarationsResultDto.md)

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


## taxPortalAccountSetIdTasksGet

> TaxTasksResultDto taxPortalAccountSetIdTasksGet(accountSetId, from, to)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdTasksGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Date (optional)
    from: 2013-10-20,
    // Date (optional)
    to: 2013-10-20,
  } satisfies TaxPortalAccountSetIdTasksGetRequest;

  try {
    const data = await api.taxPortalAccountSetIdTasksGet(body);
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
| **from** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **to** | `Date` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**TaxTasksResultDto**](TaxTasksResultDto.md)

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


## taxPortalAccountSetIdTaxKindsGet

> TaxKindsResultDto taxPortalAccountSetIdTaxKindsGet(accountSetId)



### Example

```ts
import {
  Configuration,
  TaxPortalApi,
} from '';
import type { TaxPortalAccountSetIdTaxKindsGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new TaxPortalApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies TaxPortalAccountSetIdTaxKindsGetRequest;

  try {
    const data = await api.taxPortalAccountSetIdTaxKindsGet(body);
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

[**TaxKindsResultDto**](TaxKindsResultDto.md)

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

