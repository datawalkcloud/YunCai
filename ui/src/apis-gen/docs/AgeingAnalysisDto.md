
# AgeingAnalysisDto


## Properties

Name | Type
------------ | -------------
`meta` | [LedgerMetaDto](LedgerMetaDto.md)
`assistTypeText` | string
`asOfDate` | string
`bucketLabels` | Array&lt;string&gt;
`note` | string
`accounts` | [Array&lt;AgeingAccountDto&gt;](AgeingAccountDto.md)
`total` | number

## Example

```typescript
import type { AgeingAnalysisDto } from ''

// TODO: Update the object below with actual values
const example = {
  "meta": null,
  "assistTypeText": null,
  "asOfDate": null,
  "bucketLabels": null,
  "note": null,
  "accounts": null,
  "total": null,
} satisfies AgeingAnalysisDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as AgeingAnalysisDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


