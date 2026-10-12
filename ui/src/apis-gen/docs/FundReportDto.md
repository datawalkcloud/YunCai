
# FundReportDto


## Properties

Name | Type
------------ | -------------
`dimension` | string
`startDate` | Date
`endDate` | Date
`rows` | [Array&lt;FundReportRowDto&gt;](FundReportRowDto.md)
`total` | [FundReportRowDto](FundReportRowDto.md)

## Example

```typescript
import type { FundReportDto } from ''

// TODO: Update the object below with actual values
const example = {
  "dimension": null,
  "startDate": null,
  "endDate": null,
  "rows": null,
  "total": null,
} satisfies FundReportDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as FundReportDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


