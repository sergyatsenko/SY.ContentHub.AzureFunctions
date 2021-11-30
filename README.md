# Generating Solr index for Content Hub



## Why bother indexing Content Hub content with Solr?

[Sitecore Content Hub](https://www.sitecore.com/products/content-hub) is a unified platform, which allows to bring together and centralize all content and assets in one system. Content Hub maintains its own search index internally – this enables a number of standardized search capabilities via its interfaces and APIs, but may be sufficient in some cases, but not nearly as flexible and customizable as, say, a Solr index for Sitecore CMS. What if we need to manage company assets, products and content in Content Hub, but in addition to simply showing these on the website, we need to make them searchable via rich custom UI, where results can be faceted, sorted, boosted, etc.?  [Here's one example](https://www.avalonwaterways.com/river-cruises/danube-river/?computed_rivers_sm=Danube&format=vertical&limit=12&page=0&sort=computed_itinerary_firstbestdiscountedprice_usd_tl%3Dasc) of such rich search interface, this follows a typical pattern where company offerings can be searched for, filtered, paged, and sorted based on somewhat complex business requirements – for this kind of requirements Content Hub's out-of-the-box 

![Sitecore CMP architecture](Generating Solr index for Content Hub.assets/160e349151e759.png)

One solution is to use Content Hub as the source of truth for assets, products and content and then have them synchronized to, say, [Sitecore CMS via Sitecore Connect for CMP](https://doc.sitecore.com/en/developers/sitecore-cmp/30/sitecore-connect-for-sitecore-cmp/sitecore-connect-for-sitecore-cmp.html), in which case everything is synchronized to Sitecore CMS, which in turn makes it available on the web and to 3rd party apps and services, like so

![cass basic diagram showing flow from sitecore content hub through api to consumption in channels: caas-basic-diagram.png](Generating Solr index for Content Hub.assets/EE027607-0CB6-41BB-82BF-112C9E81097B.png)

This is exciting, as there's no more need for all this constant syncing to Sitecore CMS. The only challenge is search: the GraphQL API in its current form does not provide rich search capabilities to power rich custom search UI, like [this one](https://www.avalonwaterways.com/river-cruises/danube-river/?computed_rivers_sm=Danube&format=vertical&limit=12&page=0&sort=computed_itinerary_firstbestdiscountedprice_usd_tl%3Dasc). And what is the solution? To create a custom index for Content Hub. This may sound like a lot of effort, but Content Hub's OOTB API hooks and Azure Logic apps where custom and default Azure functions can be combined make things much easier than writing it from scratch…

## Creating Solr Indexer for Content Hub with Azure Logic apps and Functions 

### High-level architecture

I fell in love with [Azure Logic apps](https://docs.microsoft.com/en-us/azure/logic-apps/logic-apps-overview) and [functions](https://docs.microsoft.com/en-us/azure/azure-functions/functions-overview), so I chose them as a hosting mechanism and I believe. It's cloud-native and very cost-effective hosting mechanism where deployment and hosting overhead is low. I've built few custom Azure functions for Content Hub and JSON processing specific tasks and leveraged some of those provided by the platform whwere possible.

Here's high-level architecture diagram describing how these main components work together

TBD

### Azure Logic App to index Content Hub content

Below diagram is taken from Azure logic App designer for my sample indexer. I like this very visual way of coding apps - easy to understand and easy to maintain. This app is POC to illustrate the idea, not quite prodction ready, but it works and can easily be improved with Azure queues, retry and poison message handling logic, enhanced logging with App insights and so on, but that would make this blog post very long :)

Here's how it works: once triggered an App will read affected entity from Content Hub via its REST API using "Get Content Hub Entity Data", then generate Solr payload with "Render JSON Template" and POST to the Solr server. 

![image-20211129174621285](Generating Solr index for Content Hub.assets/image-20211129174621285-8233184.png)

### Details on Azure Functions

##### When a HTTP request is received

First item is an entry point of this entire Azure Logic App, It's an abstraction, defining entry point URL and payload JSON schema, which is what Content Hub's "API call" action will invoke each time when target entity created or deleted (more on this in sections below). An ID of target Entity from Content Hub is getting passed in payload of this request – it will be used to retrieve that Entity in functions/steps below.    

![image-20211129175740810](Generating Solr index for Content Hub.assets/image-20211129175740810-8233862.png)  

##### Get Content Hub Entity Data

Next funcion is a custom one: it makes an API call to Content Hub REST API to read the target entity, as well as all related entities, listed in EntityRelations header parameter. The TargetEntityIdJsonPath parameter is a [JSON Path](https://support.smartbear.com/alertsite/docs/monitors/api/endpoint/jsonpath.html) exrpression, pointing to where actual ID of target entity can be found in payload JSON. For the purpose of this POC I simply passed Conetnt Hub credentials via function header parameters – in real-life production scenarios such information should be stored in configs or, even better, [Azure Key vault](https://docs.microsoft.com/en-us/azure/key-vault/general/basic-concepts). 

![image-20211129181357459](Generating Solr index for Content Hub.assets/image-20211129181357459-8234838.png)

Internally this function will read all properties from target entities, then read and add all fields from specified related entities and finally append [renditions](https://docs.stylelabs.com/contenthub/4.1.x/content/integrations/sdk-common-documentation/entity/renditions.html?rp=true) of target entity. The output is serialized into JSON, which looks like this:

{

​	"Properties": { /* target entity properties */},

​	"Properties": { /* a collection of elements, holding properties of the related entities, specified in EntityRelations parameter above */},

​	"Renditions": { /* collection of rendition names and their Urls in Content Hub */}

} 

See additional sections below for more details on function code and link to code project in github 

#### Render JSON Template

![image-20211129183012532](Generating Solr index for Content Hub.assets/image-20211129183012532-8235814.png)

Another custom function, which is a very simple version of templating engine. The template is expected to have two kinds of tokens: 

* Tokens enclosed in double curly brackets hold JSON path to value, which should be found in Entity data from previous call and then injected in place of given token. Here's how it may look like {{$.Properties.Id}}
* Tokens enclosed in double square brackets can have the actual values to be injected in place of given token. Those can look like this: [[ Hello world ]]

 I used simple Regex to find and then replace all in template.  See additional sections below for more details on function code and link to code project in github

##### Update Solr Index

This is an out-of-the-box HTTP POST call to Solr server to update an index. It takes an output of the above "Render JSON Template" function and forwards it to Solr. Note the Authorization header: this often is the case to have Solr servers be protected with basic authentication in production environments. The value of Authorization header is username:password string encoded as a base64 string

![image-20211129183502137](Generating Solr index for Content Hub.assets/image-20211129183502137-8236103.png) 

##### Response

Last element is to format and send response to the caller. I simply forwarder the response body from the above call to Solr since calling action in Content Hub doesn't really care about the response. 

![image-20211129183605811](Generating Solr index for Content Hub.assets/image-20211129183605811-8236168.png)

### Using Content Hub Actions and Triggers to call above Logic App

Please refer to [Sitecore documentation](https://docs.stylelabs.com/contenthub/4.1.x/content/integrations/integration-components/triggers/overview.html) for more details. 

---



Appendix: Source code: (insert link)
