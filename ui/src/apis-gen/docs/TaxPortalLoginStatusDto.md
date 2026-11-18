
# TaxPortalLoginStatusDto


## Properties

Name | Type
------------ | -------------
`state` | string
`message` | string
`sessionId` | string
`captcha` | [TaxPortalCaptchaDto](TaxPortalCaptchaDto.md)
`qrImage` | string
`failureShot` | string

## Example

```typescript
import type { TaxPortalLoginStatusDto } from ''

// TODO: Update the object below with actual values
const example = {
  "state": null,
  "message": null,
  "sessionId": null,
  "captcha": null,
  "qrImage": null,
  "failureShot": null,
} satisfies TaxPortalLoginStatusDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as TaxPortalLoginStatusDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


