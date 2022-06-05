# CDN

## role

this is the content delivery network of Versine, it stores and deletes images

# ⚠️⚠️⚠️ CDN shouldn't have access to the internet, only to the other Versine microservices intranet. IT STORES AND DELETES IMAGES WITHOUT VERIFYING THE AUTHOR OF THE REQUEST

## usage

### add a file to the CDN

#### endpoint 

/addFile

#### method

POST

#### body

{
  "data":"image_file_encoded_to_base64_string"
}

#### server response

success :

{
	"status": "success",
	"message": "saved file",
	"data": "id_of_the_file"
}

fail :

{
	"status": "fail",
	"message": "error_message",
  "data": null
}

### delete a file from the CDN

#### endpoint 

/deleteFile

#### method

POST

#### body

{
  "id":"id_of_the_image_to_delete"
}

#### server response

success :

{
	"status": "success",
	"message": "file deleted",
	"data": ""
}

or

{
	"status": "success",
	"message": "file doesn't exist",
	"data": ""
}

fail :

{
	"status": "fail",
	"message": "couldn't delete file",
  "data": null
}


## configuration

this microservice is configured with the file appsettings.json.
a template file is provided
