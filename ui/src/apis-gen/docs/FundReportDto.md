
# FundReportDto


## Properties

Name | Type
------------ | -------------
`dimension` | string
`startDate` | Date
`endDate` | Date
`sections` | [Array&lt;FundReportSectionDto&gt;](FundReportSectionDto.md)
`total` | [FundReportRowDto](FundReportRowDto.md)
`note` | string
`currencyNote` | string
`supportsAccountFilter` | boolean
`primaryText` | string
`secondaryText` | string

## Example

```typescript
import type { FundReportDto } from ''

// TODO: Update the object below with actual values
const example = {
  "dimension": null,
  "startDate": null,
  "endDate": null,
  "sections": null,
  "total": null,
  "note": null,
  "currencyNote": null,
  "supportsAccountFilter": null,
  "primaryText": null,
  "secondaryText": null,
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


