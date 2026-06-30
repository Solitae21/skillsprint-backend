import express from "express";

const app = express();

app.get("/", (req, res) => {
  return res.send("Hello world");
});

app.post("/api/post", (req, res) => {
  console.log(req.body);

  return res.sendStatus(200);
});

app.listen(3000, () => {
  console.log("application listening");
});
