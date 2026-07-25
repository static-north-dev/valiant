### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
VLE001 | Valiant.Endpoints | Error | Endpoint declaration must implement IEndpoint.
VLE002 | Valiant.Endpoints | Error | Endpoint declaration must not implement both IEndpoint and IEndpointGroup.
VLE003 | Valiant.Endpoints | Error | Endpoint group declarations must use ValiantEndpointGroupAttribute instead of ValiantEndpointAttribute.
VLE004 | Valiant.Endpoints | Error | Endpoint parent group type must implement IEndpointGroup.
VLE005 | Valiant.Endpoints | Error | Endpoint group declaration must implement IEndpointGroup.
VLE006 | Valiant.Endpoints | Error | Endpoint declaration must be marked with ValiantEndpointAttribute.
VLE007 | Valiant.Endpoints | Error | Endpoint group declaration must be marked with ValiantEndpointGroupAttribute.
VLE008 | Valiant.Endpoints | Error | Endpoint declaration must use ValiantEndpointAttribute instead of ValiantEndpointGroupAttribute.
VLE009 | Valiant.Endpoints | Error | Endpoint declaration must use only one ValiantEndpointAttribute form.
VLE010 | Valiant.Endpoints | Error | Query object types discovered from Valiant endpoint request models must support generated partial query binding.
VLE011 | Valiant.Endpoints | Error | Direct query parameters using the same generated parser type must use one JsonValues policy.
