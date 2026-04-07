
---LLM (OLLAMA)---

-install ollama 
ollama pull llama3.1
ollama pull all-minilm

---LLM (OLLAMA)---

---DOCKER DEAMON ON WSL---

sudo nano /etc/docker/daemon.json
{
  "insecure-registries" : ["registry-1.docker.io", "auth.docker.io", "production.cloudflare.docker.com"]
}

certficitates (get on any page from chrome - get root certficiate)
sudo cp /mnt/c/ROOT_FIRMY.crt /usr/local/share/ca-certificates/
sudo update-ca-certificates
sudo service docker restart
sudo apt-get install -y docker.io && sudo usermod -aG docker $USER && sudo service docker start

---DOCKER DEAMON ON WSL---


---DATABASE FOR VECTORS---

docker run -d --name pgvector -e POSTGRES_PASSWORD=password -p 7777:5432 ankane/pgvector 
--(it will run on docker on port 5432 but will be accessible on port 7777locally ))

---DATABASE FOR VECTORS---