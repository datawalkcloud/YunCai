
# BulkAuthenticateRequest


## Properties

Name | Type
------------ | -------------
`ids` | Array&lt;string&gt;
`authDate` | Date
`taxPeriod` | number

## Example

```typescript
import type { BulkAuthenticateRequest } from ''

// TODO: Update the object below with actual values
const example = {
  "ids": null,
  "authDate": null,
  "taxPeriod": null,
} satisfies BulkAuthenticateRequest

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as BulkAuthenticateRequest
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


