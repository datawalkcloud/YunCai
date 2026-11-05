# SalariesApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdSalaryAccrueViewGet**](SalariesApi.md#accountsetsaccountsetidsalaryaccrueviewget) | **GET** /accountsets/{accountSetId}/salary/accrue-view |  |
| [**accountsetsAccountSetIdSalaryBillsExportGet**](SalariesApi.md#accountsetsaccountsetidsalarybillsexportget) | **GET** /accountsets/{accountSetId}/salary/bills/export |  |
| [**accountsetsAccountSetIdSalaryBillsGet**](SalariesApi.md#accountsetsaccountsetidsalarybillsget) | **GET** /accountsets/{accountSetId}/salary/bills |  |
| [**accountsetsAccountSetIdSalaryBillsIdDelete**](SalariesApi.md#accountsetsaccountsetidsalarybillsiddelete) | **DELETE** /accountsets/{accountSetId}/salary/bills/{id} |  |
| [**accountsetsAccountSetIdSalaryBillsImportPost**](SalariesApi.md#accountsetsaccountsetidsalarybillsimportpost) | **POST** /accountsets/{accountSetId}/salary/bills/import |  |
| [**accountsetsAccountSetIdSalaryBillsPost**](SalariesApi.md#accountsetsaccountsetidsalarybillspost) | **POST** /accountsets/{accountSetId}/salary/bills |  |
| [**accountsetsAccountSetIdSalaryEmployeesBulkDeletePost**](SalariesApi.md#accountsetsaccountsetidsalaryemployeesbulkdeletepost) | **POST** /accountsets/{accountSetId}/salary/employees/bulk-delete |  |
| [**accountsetsAccountSetIdSalaryEmployeesExportGet**](SalariesApi.md#accountsetsaccountsetidsalaryemployeesexportget) | **GET** /accountsets/{accountSetId}/salary/employees/export |  |
| [**accountsetsAccountSetIdSalaryEmployeesGet**](SalariesApi.md#accountsetsaccountsetidsalaryemployeesget) | **GET** /accountsets/{accountSetId}/salary/employees |  |
| [**accountsetsAccountSetIdSalaryEmployeesIdDelete**](SalariesApi.md#accountsetsaccountsetidsalaryemployeesiddelete) | **DELETE** /accountsets/{accountSetId}/salary/employees/{id} |  |
| [**accountsetsAccountSetIdSalaryEmployeesIdPut**](SalariesApi.md#accountsetsaccountsetidsalaryemployeesidput) | **PUT** /accountsets/{accountSetId}/salary/employees/{id} |  |
| [**accountsetsAccountSetIdSalaryEmployeesImportPost**](SalariesApi.md#accountsetsaccountsetidsalaryemployeesimportpost) | **POST** /accountsets/{accountSetId}/salary/employees/import |  |
| [**accountsetsAccountSetIdSalaryEmployeesImportTemplateGet**](SalariesApi.md#accountsetsaccountsetidsalaryemployeesimporttemplateget) | **GET** /accountsets/{accountSetId}/salary/employees/import-template |  |
| [**accountsetsAccountSetIdSalaryEmployeesPost**](SalariesApi.md#accountsetsaccountsetidsalaryemployeespost) | **POST** /accountsets/{accountSetId}/salary/employees |  |
| [**accountsetsAccountSetIdSalaryEmployeesSyncFromIitPost**](SalariesApi.md#accountsetsaccountsetidsalaryemployeessyncfromiitpost) | **POST** /accountsets/{accountSetId}/salary/employees/sync-from-iit |  |
| [**accountsetsAccountSetIdSalaryGenerateAccruePost**](SalariesApi.md#accountsetsaccountsetidsalarygenerateaccruepost) | **POST** /accountsets/{accountSetId}/salary/generate-accrue |  |
| [**accountsetsAccountSetIdSalaryGeneratePayPost**](SalariesApi.md#accountsetsaccountsetidsalarygeneratepaypost) | **POST** /accountsets/{accountSetId}/salary/generate-pay |  |
| [**accountsetsAccountSetIdSalaryInsuranceGet**](SalariesApi.md#accountsetsaccountsetidsalaryinsuranceget) | **GET** /accountsets/{accountSetId}/salary/insurance |  |
| [**accountsetsAccountSetIdSalaryInsuranceIdDelete**](SalariesApi.md#accountsetsaccountsetidsalaryinsuranceiddelete) | **DELETE** /accountsets/{accountSetId}/salary/insurance/{id} |  |
| [**accountsetsAccountSetIdSalaryInsuranceIdPut**](SalariesApi.md#accountsetsaccountsetidsalaryinsuranceidput) | **PUT** /accountsets/{accountSetId}/salary/insurance/{id} |  |
| [**accountsetsAccountSetIdSalaryInsurancePost**](SalariesApi.md#accountsetsaccountsetidsalaryinsurancepost) | **POST** /accountsets/{accountSetId}/salary/insurance |  |
| [**accountsetsAccountSetIdSalaryPayslipsPdfGet**](SalariesApi.md#accountsetsaccountsetidsalarypayslipspdfget) | **GET** /accountsets/{accountSetId}/salary/payslips/pdf |  |
| [**accountsetsAccountSetIdSalarySummaryExportGet**](SalariesApi.md#accountsetsaccountsetidsalarysummaryexportget) | **GET** /accountsets/{accountSetId}/salary/summary/export |  |
| [**accountsetsAccountSetIdSalarySummaryGet**](SalariesApi.md#accountsetsaccountsetidsalarysummaryget) | **GET** /accountsets/{accountSetId}/salary/summary |  |
| [**accountsetsAccountSetIdSalaryVoucherSettingGet**](SalariesApi.md#accountsetsaccountsetidsalaryvouchersettingget) | **GET** /accountsets/{accountSetId}/salary/voucher-setting |  |
| [**accountsetsAccountSetIdSalaryVoucherSettingPut**](SalariesApi.md#accountsetsaccountsetidsalaryvouchersettingput) | **PUT** /accountsets/{accountSetId}/salary/voucher-setting |  |



## accountsetsAccountSetIdSalaryAccrueViewGet

> AccrueViewDto accountsetsAccountSetIdSalaryAccrueViewGet(accountSetId, period)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryAccrueViewGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    period: 56,
  } satisfies AccountsetsAccountSetIdSalaryAccrueViewGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryAccrueViewGet(body);
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
| **period** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**AccrueViewDto**](AccrueViewDto.md)

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


## accountsetsAccountSetIdSalaryBillsExportGet

> accountsetsAccountSetIdSalaryBillsExportGet(accountSetId, period)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryBillsExportGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    period: 56,
  } satisfies AccountsetsAccountSetIdSalaryBillsExportGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryBillsExportGet(body);
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
| **period** | `number` |  | [Optional] [Defaults to `undefined`] |

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


## accountsetsAccountSetIdSalaryBillsGet

> Array&lt;SalaryBillDto&gt; accountsetsAccountSetIdSalaryBillsGet(accountSetId, period)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryBillsGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    period: 56,
  } satisfies AccountsetsAccountSetIdSalaryBillsGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryBillsGet(body);
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
| **period** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**Array&lt;SalaryBillDto&gt;**](SalaryBillDto.md)

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


## accountsetsAccountSetIdSalaryBillsIdDelete

> accountsetsAccountSetIdSalaryBillsIdDelete(accountSetId, id)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryBillsIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdSalaryBillsIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryBillsIdDelete(body);
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


## accountsetsAccountSetIdSalaryBillsImportPost

> SalaryBillImportResultDto accountsetsAccountSetIdSalaryBillsImportPost(accountSetId, period, file)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryBillsImportPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    period: 56,
    // Blob (optional)
    file: BINARY_DATA_HERE,
  } satisfies AccountsetsAccountSetIdSalaryBillsImportPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryBillsImportPost(body);
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
| **period** | `number` |  | [Optional] [Defaults to `undefined`] |
| **file** | `Blob` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**SalaryBillImportResultDto**](SalaryBillImportResultDto.md)

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


## accountsetsAccountSetIdSalaryBillsPost

> SalaryBillDto accountsetsAccountSetIdSalaryBillsPost(accountSetId, saveSalaryBillRequest)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryBillsPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveSalaryBillRequest
    saveSalaryBillRequest: ...,
  } satisfies AccountsetsAccountSetIdSalaryBillsPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryBillsPost(body);
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
| **saveSalaryBillRequest** | [SaveSalaryBillRequest](SaveSalaryBillRequest.md) |  | |

### Return type

[**SalaryBillDto**](SalaryBillDto.md)

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


## accountsetsAccountSetIdSalaryEmployeesBulkDeletePost

> SalaryBulkResultDto accountsetsAccountSetIdSalaryEmployeesBulkDeletePost(accountSetId, salaryBulkIdsRequest)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryEmployeesBulkDeletePostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SalaryBulkIdsRequest
    salaryBulkIdsRequest: ...,
  } satisfies AccountsetsAccountSetIdSalaryEmployeesBulkDeletePostRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryEmployeesBulkDeletePost(body);
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
| **salaryBulkIdsRequest** | [SalaryBulkIdsRequest](SalaryBulkIdsRequest.md) |  | |

### Return type

[**SalaryBulkResultDto**](SalaryBulkResultDto.md)

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


## accountsetsAccountSetIdSalaryEmployeesExportGet

> accountsetsAccountSetIdSalaryEmployeesExportGet(accountSetId)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryEmployeesExportGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdSalaryEmployeesExportGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryEmployeesExportGet(body);
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


## accountsetsAccountSetIdSalaryEmployeesGet

> Array&lt;EmployeeDto&gt; accountsetsAccountSetIdSalaryEmployeesGet(accountSetId)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryEmployeesGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdSalaryEmployeesGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryEmployeesGet(body);
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

[**Array&lt;EmployeeDto&gt;**](EmployeeDto.md)

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


## accountsetsAccountSetIdSalaryEmployeesIdDelete

> accountsetsAccountSetIdSalaryEmployeesIdDelete(accountSetId, id)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryEmployeesIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdSalaryEmployeesIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryEmployeesIdDelete(body);
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


## accountsetsAccountSetIdSalaryEmployeesIdPut

> EmployeeDto accountsetsAccountSetIdSalaryEmployeesIdPut(accountSetId, id, saveEmployeeRequest)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryEmployeesIdPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveEmployeeRequest
    saveEmployeeRequest: ...,
  } satisfies AccountsetsAccountSetIdSalaryEmployeesIdPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryEmployeesIdPut(body);
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
| **saveEmployeeRequest** | [SaveEmployeeRequest](SaveEmployeeRequest.md) |  | |

### Return type

[**EmployeeDto**](EmployeeDto.md)

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


## accountsetsAccountSetIdSalaryEmployeesImportPost

> EmployeeImportResultDto accountsetsAccountSetIdSalaryEmployeesImportPost(accountSetId, file)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryEmployeesImportPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Blob (optional)
    file: BINARY_DATA_HERE,
  } satisfies AccountsetsAccountSetIdSalaryEmployeesImportPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryEmployeesImportPost(body);
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

[**EmployeeImportResultDto**](EmployeeImportResultDto.md)

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


## accountsetsAccountSetIdSalaryEmployeesImportTemplateGet

> accountsetsAccountSetIdSalaryEmployeesImportTemplateGet(accountSetId)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryEmployeesImportTemplateGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdSalaryEmployeesImportTemplateGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryEmployeesImportTemplateGet(body);
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


## accountsetsAccountSetIdSalaryEmployeesPost

> EmployeeDto accountsetsAccountSetIdSalaryEmployeesPost(accountSetId, saveEmployeeRequest)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryEmployeesPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveEmployeeRequest
    saveEmployeeRequest: ...,
  } satisfies AccountsetsAccountSetIdSalaryEmployeesPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryEmployeesPost(body);
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
| **saveEmployeeRequest** | [SaveEmployeeRequest](SaveEmployeeRequest.md) |  | |

### Return type

[**EmployeeDto**](EmployeeDto.md)

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


## accountsetsAccountSetIdSalaryEmployeesSyncFromIitPost

> EmployeeImportResultDto accountsetsAccountSetIdSalaryEmployeesSyncFromIitPost(accountSetId, iitPersonnelSyncRequest)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryEmployeesSyncFromIitPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // IitPersonnelSyncRequest
    iitPersonnelSyncRequest: ...,
  } satisfies AccountsetsAccountSetIdSalaryEmployeesSyncFromIitPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryEmployeesSyncFromIitPost(body);
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
| **iitPersonnelSyncRequest** | [IitPersonnelSyncRequest](IitPersonnelSyncRequest.md) |  | |

### Return type

[**EmployeeImportResultDto**](EmployeeImportResultDto.md)

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


## accountsetsAccountSetIdSalaryGenerateAccruePost

> GenerateCountDto accountsetsAccountSetIdSalaryGenerateAccruePost(accountSetId, generateSalaryVoucherRequest)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryGenerateAccruePostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // GenerateSalaryVoucherRequest
    generateSalaryVoucherRequest: ...,
  } satisfies AccountsetsAccountSetIdSalaryGenerateAccruePostRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryGenerateAccruePost(body);
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
| **generateSalaryVoucherRequest** | [GenerateSalaryVoucherRequest](GenerateSalaryVoucherRequest.md) |  | |

### Return type

[**GenerateCountDto**](GenerateCountDto.md)

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


## accountsetsAccountSetIdSalaryGeneratePayPost

> GenerateCountDto accountsetsAccountSetIdSalaryGeneratePayPost(accountSetId, generateSalaryVoucherRequest)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryGeneratePayPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // GenerateSalaryVoucherRequest
    generateSalaryVoucherRequest: ...,
  } satisfies AccountsetsAccountSetIdSalaryGeneratePayPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryGeneratePayPost(body);
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
| **generateSalaryVoucherRequest** | [GenerateSalaryVoucherRequest](GenerateSalaryVoucherRequest.md) |  | |

### Return type

[**GenerateCountDto**](GenerateCountDto.md)

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


## accountsetsAccountSetIdSalaryInsuranceGet

> Array&lt;InsuranceItemDto&gt; accountsetsAccountSetIdSalaryInsuranceGet(accountSetId)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryInsuranceGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdSalaryInsuranceGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryInsuranceGet(body);
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

[**Array&lt;InsuranceItemDto&gt;**](InsuranceItemDto.md)

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


## accountsetsAccountSetIdSalaryInsuranceIdDelete

> accountsetsAccountSetIdSalaryInsuranceIdDelete(accountSetId, id)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryInsuranceIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdSalaryInsuranceIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryInsuranceIdDelete(body);
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


## accountsetsAccountSetIdSalaryInsuranceIdPut

> InsuranceItemDto accountsetsAccountSetIdSalaryInsuranceIdPut(accountSetId, id, saveInsuranceRequest)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryInsuranceIdPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveInsuranceRequest
    saveInsuranceRequest: ...,
  } satisfies AccountsetsAccountSetIdSalaryInsuranceIdPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryInsuranceIdPut(body);
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
| **saveInsuranceRequest** | [SaveInsuranceRequest](SaveInsuranceRequest.md) |  | |

### Return type

[**InsuranceItemDto**](InsuranceItemDto.md)

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


## accountsetsAccountSetIdSalaryInsurancePost

> InsuranceItemDto accountsetsAccountSetIdSalaryInsurancePost(accountSetId, saveInsuranceRequest)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryInsurancePostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveInsuranceRequest
    saveInsuranceRequest: ...,
  } satisfies AccountsetsAccountSetIdSalaryInsurancePostRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryInsurancePost(body);
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
| **saveInsuranceRequest** | [SaveInsuranceRequest](SaveInsuranceRequest.md) |  | |

### Return type

[**InsuranceItemDto**](InsuranceItemDto.md)

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


## accountsetsAccountSetIdSalaryPayslipsPdfGet

> accountsetsAccountSetIdSalaryPayslipsPdfGet(accountSetId, period)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryPayslipsPdfGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    period: 56,
  } satisfies AccountsetsAccountSetIdSalaryPayslipsPdfGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryPayslipsPdfGet(body);
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
| **period** | `number` |  | [Optional] [Defaults to `undefined`] |

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


## accountsetsAccountSetIdSalarySummaryExportGet

> accountsetsAccountSetIdSalarySummaryExportGet(accountSetId, startPeriod, endPeriod)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalarySummaryExportGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    startPeriod: 56,
    // number (optional)
    endPeriod: 56,
  } satisfies AccountsetsAccountSetIdSalarySummaryExportGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalarySummaryExportGet(body);
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
| **startPeriod** | `number` |  | [Optional] [Defaults to `undefined`] |
| **endPeriod** | `number` |  | [Optional] [Defaults to `undefined`] |

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


## accountsetsAccountSetIdSalarySummaryGet

> Array&lt;SalarySummaryRowDto&gt; accountsetsAccountSetIdSalarySummaryGet(accountSetId, startPeriod, endPeriod)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalarySummaryGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    startPeriod: 56,
    // number (optional)
    endPeriod: 56,
  } satisfies AccountsetsAccountSetIdSalarySummaryGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalarySummaryGet(body);
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
| **startPeriod** | `number` |  | [Optional] [Defaults to `undefined`] |
| **endPeriod** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**Array&lt;SalarySummaryRowDto&gt;**](SalarySummaryRowDto.md)

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


## accountsetsAccountSetIdSalaryVoucherSettingGet

> SalaryVoucherSettingDto accountsetsAccountSetIdSalaryVoucherSettingGet(accountSetId)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryVoucherSettingGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdSalaryVoucherSettingGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryVoucherSettingGet(body);
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

[**SalaryVoucherSettingDto**](SalaryVoucherSettingDto.md)

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


## accountsetsAccountSetIdSalaryVoucherSettingPut

> SalaryVoucherSettingDto accountsetsAccountSetIdSalaryVoucherSettingPut(accountSetId, saveSalaryVoucherSettingRequest)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryVoucherSettingPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveSalaryVoucherSettingRequest
    saveSalaryVoucherSettingRequest: ...,
  } satisfies AccountsetsAccountSetIdSalaryVoucherSettingPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryVoucherSettingPut(body);
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
| **saveSalaryVoucherSettingRequest** | [SaveSalaryVoucherSettingRequest](SaveSalaryVoucherSettingRequest.md) |  | |

### Return type

[**SalaryVoucherSettingDto**](SalaryVoucherSettingDto.md)

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

