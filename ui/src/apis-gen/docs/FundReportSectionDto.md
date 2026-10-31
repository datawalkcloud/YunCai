
# FundReportSectionDto


## Properties

Name | Type
------------ | -------------
`name` | string
`subtotalLabel` | string
`rows` | [Array&lt;FundReportRowDto&gt;](FundReportRowDto.md)
`subtotal` | [FundReportRowDto](FundReportRowDto.md)

## Example

```typescript
import type { FundReportSectionDto } from ''

// TODO: Update the object below with actual values
const example = {
  "name": null,
  "subtotalLabel": null,
  "rows": null,
  "subtotal": null,
} satisfies FundReportSectionDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as FundReportSectionDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


