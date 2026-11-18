
# TaxPortalBrowserHandoffDto


## Properties

Name | Type
------------ | -------------
`url` | string
`cookies` | [Array&lt;TaxPortalBrowserCookieDto&gt;](TaxPortalBrowserCookieDto.md)
`origins` | [Array&lt;TaxPortalBrowserOriginDto&gt;](TaxPortalBrowserOriginDto.md)

## Example

```typescript
import type { TaxPortalBrowserHandoffDto } from ''

// TODO: Update the object below with actual values
const example = {
  "url": null,
  "cookies": null,
  "origins": null,
} satisfies TaxPortalBrowserHandoffDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as TaxPortalBrowserHandoffDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


