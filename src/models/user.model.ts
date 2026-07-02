import { Role } from "@/enums/role.enum";
import { Schema, model } from "mongoose";

const userSchema = new Schema(
  {
    email: {
      type: String,
      required: true,
      unique: true,
      lowercase: true,
      trim: true,
      index: true,
    },
    passwordHash: { type: String, required: true, select: false }, // never returned by default
    name: { type: String, required: true, trim: true },
    role: {
      type: String,
      enum: Role,
      default: "student",
    },
    refreshTokenHashes: { type: [String], default: [], select: false }, // hashed, for rotation/revoke
  },
  { timestamps: true },
);

// Strip sensitive/internal fields from any JSON response
userSchema.set("toJSON", {
  transform: (_doc, ret) => {
    const { passwordHash: _p, refreshTokenHashes: _r, __v: _v, ...sanitized } = ret as Record<string, unknown>;
    return sanitized;
  },
});

export const User = model("User", userSchema);
