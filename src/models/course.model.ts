import { Schema, model } from "mongoose";

const courseSchema = new Schema(
  {
    slug: { type: String, required: true, unique: true, index: true }, // this maps to frontend id
    courseName: { type: String, required: true, trim: true },
    instructor: { type: String, required: true, trim: true },
    thumbnail: { type: String, required: true },
    description: { type: String, default: "" },
    categories: { type: [String], default: [] },
    topics: { type: [String], default: [] },
  },
  { timestamps: true },
);

courseSchema.index({ courseName: "text" }); // this allows quicker text search because it is now indexed

export const Course = model("Course", courseSchema);
